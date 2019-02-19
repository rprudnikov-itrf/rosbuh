using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Thrift.Transport;
using Thrift.Protocol;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using RosService.Configuration;

namespace RosService.HBaseCore
{
    static class ClientHbase
    {
        static int Port = 9090;
        static List<string> defaul_columnFamilys = new string[] { "Node", "Href", "Settings" }.ToList();

        #region Получаем имя Аттрибута для Hbase
        static ConcurrentDictionary<string, byte[]> cache_attrName = new ConcurrentDictionary<string, byte[]>();

        static byte[] getAttrNameHbase(string attrName)
        {
            if (cache_attrName.ContainsKey(attrName))
                return cache_attrName[attrName];

            var val = Dal.GetBytes(attrName);
            cache_attrName.AddOrUpdate(attrName, val, (k, e) => e = val);

            return cache_attrName[attrName];
        }
        #endregion

        #region Получаем имя Host`а из конфиг файла
        static ConcurrentDictionary<string, string> cache_Host = new ConcurrentDictionary<string, string>();

        static string getHost(string baseName, string columnFamily)
        {
            var key = baseName + columnFamily;

            if (cache_Host.ContainsKey(key))
                return cache_Host[key];

            var val = "";

            // # брокер24.hbase.Node
            if (ConfigurationManager.AppSettings[baseName + ".hbase." + columnFamily] != null)
                val = ConfigurationManager.AppSettings[baseName + ".hbase." + columnFamily];
            // # брокер24.hbase
            else if (ConfigurationManager.AppSettings[baseName + ".hbase"] != null)
                val = ConfigurationManager.AppSettings[baseName + ".hbase"];
            // # default.hbase.Node
            else if (ConfigurationManager.AppSettings["default.hbase." + columnFamily] != null)
                val = ConfigurationManager.AppSettings["default.hbase." + columnFamily];
            // # default.hbase
            else
                val = ConfigurationManager.AppSettings["default.hbase"];

            cache_Host.AddOrUpdate(key, val, (k, e) => e = val);

            return cache_Host[key];
        }
        #endregion

        #region В Hbase имя базы данных строго на латинице и чтоб постоянно его не резолвить делаем кеш
        static ConcurrentDictionary<string, byte[]> cache_baseName = new ConcurrentDictionary<string, byte[]>();

        static byte[] getBaseTableHbase(string baseName, string columnFamily)
        {
            var key = baseName + columnFamily;

            if (cache_baseName.ContainsKey(key))
                return cache_baseName[key];

            var val = Dal.GetBytes(Dal.РусскийВТранслит(key));
            cache_baseName.AddOrUpdate(key, val, (k, e) => e = val);

            return cache_baseName[key];
        }
        #endregion

        #region Создание и удаление базы данных
        // Создание типовой базы
        public static void CreateDataBase(string baseName)
        {
            foreach (var columnFamily in defaul_columnFamilys)
                _CreateDataBase(baseName, columnFamily); 
        }

        static void _CreateDataBase(string baseName, string columnFamily)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return;
            if (string.IsNullOrWhiteSpace(columnFamily)) return;

            var socket = null as TSocket;
            var transport = null as TBufferedTransport;
            try
            {
                // Сделаем вычисление апи хоста по базе данных 
                socket = new TSocket(getHost(baseName, columnFamily), Port);
                transport = new TBufferedTransport(socket);
                var proto = new TBinaryProtocol(transport);
                var hbase = new Hbase.Client(proto);

                transport.Open();

                hbase.createTable(
                    getBaseTableHbase(baseName, columnFamily),
                    new List<ColumnDescriptor>() 
                    {
                        new ColumnDescriptor { Name = getAttrNameHbase(columnFamily), BlockCacheEnabled = true, MaxVersions = 1, InMemory = true }
                    });
            }
            finally
            {
                if (transport != null)
                {
                    transport.Close();
                    transport = null;
                }
            }
        }

        // Удаление базы
        public static void DropDataBase(string baseName)
        {
            foreach (var columnFamily in defaul_columnFamilys)
                _DropDataBase(baseName, columnFamily); 
        }

        static void _DropDataBase(string baseName, string columnFamily)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return;
            if (string.IsNullOrWhiteSpace(columnFamily)) return;

            var socket = null as TSocket;
            var transport = null as TBufferedTransport;
            try
            {
                // Сделаем вычисление апи хоста по базе данных 
                socket = new TSocket(getHost(baseName, columnFamily), Port);
                transport = new TBufferedTransport(socket);
                var proto = new TBinaryProtocol(transport);
                var hbase = new Hbase.Client(proto);

                transport.Open();

                var tableName = getBaseTableHbase(baseName, columnFamily);

                if (hbase.isTableEnabled(tableName))
                {
                    hbase.disableTable(tableName);
                    hbase.deleteTable(tableName);
                }
            }            
            finally
            {
                if (transport != null)
                {
                    transport.Close();
                    transport = null;
                }
            }
        }
        #endregion

        #region SET - Сохранение данных и ссылок (ИдентификаторовОбъектов)
        static object lock_SetAsinc = new object();
        public static int countProcessSetAsinc = 0;

        public static void SetAsinc(string baseName, Row row)
        {
            SetsAsinc(baseName, new[] { row }.ToList());
        }

        public static void SetsAsinc(string baseName, List<Row> rows)
        {
            var transaction_date = DateTime.Now;

            System.Threading.Tasks.Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        System.Threading.Interlocked.Increment(ref countProcessSetAsinc);

                        lock (lock_SetAsinc)
                        {
                            _Sets(baseName, rows, transaction_date);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Send Error Service
                        ConfigurationClient.WindowsLog(ex.Message, "system", baseName);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Decrement(ref countProcessSetAsinc);
                    }                
                });
        }

        public static void Set(string baseName, Row row)
        {
            if (row == null) return;
            _Sets(baseName, new[] { row }.ToList(), DateTime.Now);
        }

        public static void Sets(string baseName, List<Row> rows)
        {
            _Sets(baseName, rows, DateTime.Now);
        }

        static void _Sets(string baseName, List<Row> rows, DateTime transaction_date)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return;
            if (rows == null || rows.Count == 0) return;
            if (rows.Count == 1 && rows.First().Значения.Count == 0 && rows.First().ИдентификаторыОбъекта.Count == 0) return;

            // данные для отправки в Hbase
            var dataNode = new List<global::BatchMutation>();
            var dataHref = new List<global::BatchMutation>();

            System.Threading.Tasks.Parallel.ForEach(rows, (r) =>
                {
                    // массив значений
                    if (r.Значения.Count > 0)
                    {
                        dataNode.Add(
                            new global::BatchMutation()
                            {
                                Row = r.GuidCode.ToByteArray(),
                                Mutations = r.Значения.Select(
                                                p =>
                                                new global::Mutation()
                                                {
                                                    IsDelete = false,
                                                    Column = getAttrNameHbase("Node:" + p.Key),
                                                    Value = Dal.GetBytes(p.Value)
                                                }).ToList()
                            });
                    }

                    // массив идентификаторов
                    if (r.ИдентификаторыОбъекта.Count > 0)
                    {
                        dataHref.AddRange(
                            r.ИдентификаторыОбъекта.Select(
                            k =>
                            new global::BatchMutation()
                            {
                                Row = Dal.GetBytes(k),
                                Mutations = r.Значения.Select(
                                                p =>
                                                new global::Mutation()
                                                {
                                                    IsDelete = false,
                                                    Column = getAttrNameHbase("Href:GuidCode"),
                                                    Value = r.GuidCode.ToByteArray()
                                                }).ToList()
                            }).ToList());
                    }
                });

            // отправим данные паралельно
            System.Threading.Tasks.Parallel.Invoke(
                () => { _Sets_hbase(baseName, "Node", transaction_date, dataNode); },
                () => { _Sets_hbase(baseName, "Href", transaction_date, dataHref); });
        }

        static void _Sets_hbase(string baseName, string columnFamily, DateTime transaction_date, List<global::BatchMutation> data)
        {
            var socket = null as TSocket;
            var transport = null as TBufferedTransport;

            try
            {
                // Сделаем вычисление апи хоста по базе данных 
                socket = new TSocket(getHost(baseName, columnFamily), Port);
                transport = new TBufferedTransport(socket);
                var proto = new TBinaryProtocol(transport);
                var hbase = new Hbase.Client(proto);

                var tableName = getBaseTableHbase(baseName, columnFamily);

                transport.Open();
                
                if (data.Count == 1)
                    hbase.mutateRowTs(tableName, data.First().Row, data.First().Mutations, transaction_date.Ticks, null);
                else
                    hbase.mutateRowsTs(tableName, data, transaction_date.Ticks, null);
             }
            finally
            {
                if (transport != null)
                {
                    transport.Close();
                    transport = null;
                }
            }
        }
        #endregion

        #region GET - Сохранение данных и ссылок (ИдентификаторовОбъектов)
        public static Dictionary<string, object> Get(string baseName, object id, Dictionary<string, System.Type> attrs)
        {
            if (id == null) return null;
            return Gets(baseName, new object[] { id }.ToList(), attrs)[id];
        }

        public static Dictionary<object, Dictionary<string, object>> Gets(string baseName, List<object> ids, Dictionary<string, System.Type> attrs)
        {
            if (ids == null || ids.Count == 0) return null;

            var source = new ConcurrentDictionary<object, Dictionary<string, object>>();
            var values = new ConcurrentDictionary<string, object>();

            // Заполним массив возвращаемых по умолчанию 
            System.Threading.Tasks.Parallel.ForEach(
                attrs, (item) =>
                {
                    var val = Dal.GetDefaultValue(item.Value);
                    values.AddOrUpdate(item.Key, val, (k, e) => e = val);
                });

            System.Threading.Tasks.Parallel.ForEach(
                ids, (id) =>
                {
                    var val = values.ToDictionary(k => k.Key, e => e.Value);
                    source.AddOrUpdate(id, val, (k, e) => e = val);
                });


            if (string.IsNullOrWhiteSpace(baseName)) return source.ToDictionary(k => k.Key, e => e.Value);
            if (attrs == null || attrs.Count == 0) return source.ToDictionary(k => k.Key, e => e.Value);

            // Вычисляем Guid`ы по которым нужно получить данные 
            var guids = new HashSet<Guid>(ids.Where(w => w.GetType().Equals(typeof(Guid))).Select(p => (Guid)p));
            var hrefs = ids.Where(w => !w.GetType().Equals(typeof(Guid))).Select(k => Convert.ToString(k)).ToList();
            var resolve_hrefs = new Dictionary<string, Guid>();

            // Если это не гуид то это доп идентификатор
            if (hrefs.Count > 0)
            {
                resolve_hrefs = ResolveGuidCode(baseName, hrefs);

                foreach (var g in resolve_hrefs)
                    if (!Guid.Empty.Equals(g.Value))
                        guids.Add(g.Value);
            }

            // Если Guid`ов нет то и не подключаемся к Hbase
            if(guids.Count == 0) return source.ToDictionary(k => k.Key, e => e.Value);
                        
            var socket = null as TSocket;
            var transport = null as TBufferedTransport;
            var data = new List<TRowResult>();

            // запрос данных в Hbase
            try
            {
                // Сделаем вычисление апи хоста по базе данных 
                socket = new TSocket(getHost(baseName, "Node"), Port);
                transport = new TBufferedTransport(socket);
                var proto = new TBinaryProtocol(transport);
                var hbase = new Hbase.Client(proto);

                transport.Open();

                data = hbase.getRowsWithColumns(
                                getBaseTableHbase(baseName, "Node"),
                                guids.Select(p => p.ToByteArray()).ToList(),
                                attrs.Select(p => getAttrNameHbase("Node:" + p.Key)).ToList(),
                                null);
            }
            finally
            {
                if (transport != null)
                {
                    transport.Close();
                    transport = null;
                }
            }

            // обработка полученных данных
            System.Threading.Tasks.Parallel.ForEach(
                data, (row) =>
                {
                    var GuidCode = new Guid(row.Row);
                    if (GuidCode == Guid.Empty) return;

                    var keyRows = new List<object>();

                    // Обратный Resolve для Идентификаторов
                    if (hrefs.Count > 0 && resolve_hrefs.ContainsValue(GuidCode))
                        keyRows.AddRange(resolve_hrefs.Where(w => w.Value.Equals(GuidCode)).Select(p => (object)p.Key));

                    // Если запрос по гуиду то добавляем и его
                    if (source.ContainsKey(GuidCode))
                        keyRows.Add(GuidCode);

                    System.Threading.Tasks.Parallel.ForEach(
                        keyRows, (k) =>
                        {
                            // Обратный Resolve 
                            if (!source.ContainsKey(k))
                                return;

                            System.Threading.Tasks.Parallel.ForEach(
                                row.Columns, (c) =>
                                {
                                    // нужно удалить в название аттрибута Node:
                                    var attr = System.Text.Encoding.UTF8.GetString(c.Key).Remove(0, 5);
                                    if (source[k].ContainsKey(attr))
                                        source[k][attr] = Dal.GetObject(attrs[attr], c.Value.Value);
                                });
                        });
                });

            return source.ToDictionary(k => k.Key, e => e.Value); 
        }
        #endregion

        #region RESOLVE - Резолвим GuidCode по произвольному идентификатору
        public static Guid ResolveGuidCode(string baseName, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Guid.Empty;
            return ResolveGuidCode(baseName, new string[] { id }.ToList())[id];
        }
        
        public static Dictionary<string, Guid> ResolveGuidCode(string baseName, List<string> ids)
        {
            if (ids == null || ids.Count == 0) return null;
            if (string.IsNullOrWhiteSpace(baseName)) return null; 

            var source = new ConcurrentDictionary<string, Guid>();

            System.Threading.Tasks.Parallel.ForEach(
                ids, (id) =>
                {
                    source.AddOrUpdate(id, Guid.Empty, (k, e) => e = Guid.Empty);
                });

            var socket = null as TSocket;
            var transport = null as TBufferedTransport;
            var data = new List<TRowResult>();

            // запросим данные GuidCode в Hbase
            try
            {
                // Сделаем вычисление апи хоста по базе данных 
                socket = new TSocket(getHost(baseName, "Href"), Port);
                transport = new TBufferedTransport(socket);
                var proto = new TBinaryProtocol(transport);
                var hbase = new Hbase.Client(proto);

                transport.Open();

                data = hbase.getRowsWithColumns(
                        getBaseTableHbase(baseName, "Href"),
                        ids.Select(id => Dal.GetBytes(id)).ToList(),
                        new List<byte[]>() { getAttrNameHbase("Href:GuidCode") },
                        null);
            }
            finally
            {
                if (transport != null)
                {
                    transport.Close();
                    transport = null;
                }
            }

            // обработка данных
            System.Threading.Tasks.Parallel.ForEach(
                data, (item) =>
                {
                    var key = Convert.ToString(Dal.GetObject(typeof(string), item.Row));
                    if (source.ContainsKey(key))
                        source[key] = new Guid(item.Columns.First().Value.Value);

                });

            return source.ToDictionary(k => k.Key, e => e.Value); 
        }
        #endregion
    }
}
