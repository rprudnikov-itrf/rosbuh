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
using System.Runtime.InteropServices;
using ServiceStack.Redis;

namespace RosService.Caching
{
    public class MemoryCache
    {
        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr handle, int minimumWorkingSetSize, int maximumWorkingSetSize);

        private static readonly ConcurrentDictionary<string, ValueContanier> CurrentValues;
        private static readonly Dictionary<string, ConcurrentQueue<MemoryTransaction>> Transactions;

        public static readonly System.Threading.Thread CompleteThread;
        public static volatile bool CompleteThreadStarted = true;

        #region Config
        private static TimeSpan? _Timeout;
        public static TimeSpan Timeout
        {
            get
            {
                if (_Timeout == null)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings["MemoryCache.Timeout"] == null)
                        _Timeout = TimeSpan.FromHours(3);
                    else
                        _Timeout = TimeSpan.Parse(System.Configuration.ConfigurationManager.AppSettings["MemoryCache.Timeout"]);
                }
                return _Timeout.Value;
            }
            set
            {
                if (_Timeout != value)
                {
                    _Timeout = value;
                }
            }
        }

        private static bool? _IsMemoryCacheClient;
        public static bool IsMemoryCacheClient
        {
            get
            {
                if (_IsMemoryCacheClient == null)
                {
                    _IsMemoryCacheClient = ClientRedis.Clients != null;
                }
                return _IsMemoryCacheClient.Value;
            }
        }
        #endregion

        static MemoryCache()
        {
            if (!IsMemoryCacheClient)
            {
                CurrentValues = new ConcurrentDictionary<string, ValueContanier>(Environment.ProcessorCount, 1000000);
            }

            Transactions = new Dictionary<string, ConcurrentQueue<MemoryTransaction>>();

            //запустить обработку транзакций
            //CompleteTransactions();
            CompleteThread = new System.Threading.Thread(CompleteTransactions);
            CompleteThread.Start();
        }


        private static void ClearMemory()
        {
            System.Threading.Tasks.Task.Factory.StartNew(delegate()
            {
                try
                {
                    //GC.Collect();
                    //GC.WaitForPendingFinalizers();
                    //GC.Collect();

                    //очистить память
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
                }
                catch
                {
                }
            });
        }
        public static void Clear()
        {
            //очищать память если нет внешнего memory-кеша
            if (!IsMemoryCacheClient)
            {
                var date = DateTime.Now;
                foreach (var item in CurrentValues.Where(p => !DateTime.MinValue.Equals(p.Value.cache) && p.Value.cache <= date).Select(p => p.Key).ToArray())
                {
                    var tmp = null as ValueContanier;
                    CurrentValues.TryRemove(item, out tmp);
                }
            }

            ClearMemory();
        }
        public static void Clear(string domain)
        {
            if (IsMemoryCacheClient)
            {
                foreach (var pool in ClientRedis.Clients)
                {
                    var items = new List<string>();
                    var redis = null as IRedisClient;
                    try
                    {
                        redis = pool.GetClient();
                        if (redis != null)
                        {
                            foreach (var item in redis.SearchKeys(domain + "*").Where(p => p != null && !p.StartsWith(domain + ":Z:")))
                                items.Add(item);

                            if (items.Count > 0)
                            {
                                redis.RemoveAll(items);
                            }
                        }
                    }
                    finally
                    {
                        if (pool != null && redis != null)
                            pool.DisposeClient(redis as RedisNativeClient);
                    }
                }
            }
            else
            {
                foreach (var item in CurrentValues.Where(p => p.Key != null && (p.Key.StartsWith(domain + ":D:") || p.Key.StartsWith(domain + ":C:"))).Select(p => p.Key).ToArray())
                {
                    var tmp = null as ValueContanier;
                    CurrentValues.TryRemove(item, out tmp);
                    tmp = null;
                }
            }

            ClearMemory();
        }
        public static int Count(string domain)
        {
            if (IsMemoryCacheClient)
            {
                var count = 0;
                var redis = null as IRedisClient;
                foreach (var pool in ClientRedis.Clients)
                {
                    try
                    {
                        redis = pool.GetReadOnlyClient();
                        if (redis != null)
                        {
                            count += redis.SearchKeys(domain + ":*").Count;
                        }
                    }
                    finally
                    {
                        if (pool != null && redis != null)
                            pool.DisposeReadOnlyClient(redis as RedisNativeClient);
                    }
                }
                return count;
            }
            else
            {
                return CurrentValues.Count(p => p.Key.StartsWith(domain + ":"));
            }
        }


        public static ValueContanier Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (IsMemoryCacheClient)
            {
                try
                {
                    var _tmp = ClientRedis.Get(key);
                    if (_tmp != null)
                    {
                        return new ValueContanier()
                        {
                            obj = new Value()
                            {
                                Значение = Convert.IsDBNull(_tmp) ? null : _tmp
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog("MemoryCache:Get", "", "system", key, ex.ToString());
                }
            }
            else
            {
                var _tmp = null as ValueContanier;
                if (CurrentValues.TryGetValue(key, out _tmp))
                {
                    _tmp.cache = DateTime.Now.Add(Timeout);
                    return _tmp;
                }
            }
            return null;
        }
        public static Dictionary<string, ValueContanier> Get(params string[] key)
        {
            var result = new Dictionary<string, ValueContanier>();
            if (key != null && key.Length > 0)
            {
                if (IsMemoryCacheClient)
                {
                    try
                    {
                        foreach (var item in ClientRedis.Get(key))
                        {
                            if (item.Value != null)
                            {
                                result.Add(item.Key, new ValueContanier() { obj = new Value() { Значение = Convert.IsDBNull(item.Value) ? null : item.Value } });
                            }
                        }

                        //var _tmp = ClientRedis.Get(key);
                        //if (_tmp != null)
                        //{
                        //    return new ValueContanier()
                        //    {
                        //        obj = new Value()
                        //        {
                        //            Значение = _tmp
                        //        }
                        //    };
                        //}
                    }
                    catch (Exception ex)
                    {
                        ConfigurationClient.WindowsLog("MemoryCache:Get", "", "system", key, ex.ToString());
                    }
                }
                else
                {
                    foreach (var item in key)
                    {
                        if (string.IsNullOrEmpty(item))
                            continue;

                        var _tmp = null as ValueContanier;
                        if (CurrentValues.TryGetValue(item, out _tmp))
                        {
                            _tmp.cache = DateTime.Now.Add(Timeout);
                            if (!result.ContainsKey(item))
                                result.Add(item, _tmp);
                        }
                    }
                }
            }
            return result;
        }
        public static void Set(string key, ValueContanier contanier)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (IsMemoryCacheClient)
            {
                try
                {
                    if (contanier.cache == DateTime.MinValue)
                        ClientRedis.Set(key, contanier.obj.Значение);
                    else
                        ClientRedis.Set(key, contanier.obj.Значение, contanier.cache);
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog("MemoryCache:Set", "", "system", key, ex.ToString());
                }

                //var p = key.Split(':');
                //var baseName = p[0];
                //var id_node = Convert.ToDecimal(p[1]);
                //var attrName = p[2];

                //if ("такси".Equals(baseName))
                //{
                //    var dic = new Dictionary<string, object>();
                //    dic.Add(attrName, contanier.obj.Значение);
                //    var guid = new Guid(new DataClient().ПолучитьЗначение<string>(id_node, "GuidCode", Хранилище.Оперативное, baseName));

                //    HBaseCore.ClientHbase.SetAsinc(baseName, new HBaseCore.Row(guid, dic, new string[] { id_node.ToString("f0") }.ToList()));
                //}
            }
            else
            {
                CurrentValues.AddOrUpdate(key, contanier, (k, e) => e = contanier);
            }
        }
        public static void Remove(string key)
        {
            if (IsMemoryCacheClient)
            {
                var pool = ClientRedis.GetClient(key);
                var client = null as IRedisClient;
                try
                {
                    client = pool.GetClient();
                    if (client != null)
                        client.Remove(key);
                }
                finally
                {
                    if (pool != null && client != null)
                        pool.DisposeClient(client as RedisNativeClient);
                }

                //ClientRedis.GetClient(key).Remove(key);
            }
            else
            {
                var _tmp = null as ValueContanier;
                CurrentValues.TryRemove(key, out _tmp);
            }
        }
        public static void RemoveAll(string pattern)
        {
            if (IsMemoryCacheClient)
            {
                foreach (var pool in ClientRedis.Clients)
                {
                    var items = null as List<string>;
                    var redis = null as IRedisClient;
                    try
                    {
                        redis = pool.GetClient();
                        if (redis != null)
                        {
                            items = redis.SearchKeys(pattern + "*");

                            if (items.Count > 0)
                            {
                                redis.RemoveAll(items);
                            }
                        }
                    }
                    finally
                    {
                        if (pool != null && redis != null)
                            pool.DisposeClient(redis as RedisNativeClient);
                    }
                }
            }
            else
            {
                foreach (var item in MemoryCache.CurrentValues.Where(p => p.Key != null && p.Key.StartsWith(pattern)).Select(p => p.Key).ToArray())
                {
                    Remove(item);
                }
            }
        }
        public static void RemoveAll(string pattern, string end_pattern)
        {
            if (IsMemoryCacheClient)
            {
                foreach (var pool in ClientRedis.Clients)
                {
                    var items = null as IEnumerable<string>;
                    var redis = null as IRedisClient;
                    try
                    {
                        redis = pool.GetReadOnlyClient();
                        if (redis != null)
                        {
                            items = redis.SearchKeys(pattern + "*").Where(p => p.EndsWith(end_pattern));
                            if (items.Count() > 0)
                            {
                                redis.RemoveAll(items);
                            }
                        }
                    }
                    finally
                    {
                        if (pool != null && redis != null)
                            pool.DisposeReadOnlyClient(redis as RedisNativeClient);
                    }
                }
            }
            else
            {
                foreach (var item in MemoryCache.CurrentValues
                        .Where(p => p.Key != null && p.Key.StartsWith(pattern) && p.Key.EndsWith(end_pattern))
                        .Select(p => p.Key).ToArray())
                {
                    Remove(item);
                }
            }
        }

        public static string Path(string domain, Хранилище stage, string id)
        {
            if (string.IsNullOrEmpty(id))
                return domain + Path(stage) + ":";

            return domain + Path(stage) + ":" + id + ":";
        }
        public static string Path(string domain, Хранилище stage, decimal id)
        {
            if (id == 0m)
                return domain + Path(stage) + ":";

            return domain + Path(stage) + ":" + decimal.ToInt64(id).ToString() + ":";
        }
        protected static string Path(Хранилище stage)
        {
            switch (stage)
            {
                case Хранилище.Оперативное:
                    return string.Empty;

                case Хранилище.Конфигурация:
                    return ":C";

                default:
                    return stage.ToString().Substring(0, 2);
            }
        }


        internal static object lockTransaction = new System.Object();
        internal static void AddTransaction(MemoryTransaction transaction)
        {
            if (transaction == null || string.IsNullOrEmpty(transaction.domain))
                return;

            var queue = null as ConcurrentQueue<MemoryTransaction>;
            lock (lockTransaction)
            {
                if (!Transactions.ContainsKey(transaction.domain))
                {
                    queue = new ConcurrentQueue<MemoryTransaction>();
                    Transactions.Add(transaction.domain, queue);
                }
                else
                {
                    queue = Transactions[transaction.domain];
                }
            }

            if (queue == null)
                throw new Exception("Не найден контейнер для записи в очередь");

            queue.Enqueue(transaction);
        }
        private static void CompleteTransactions()
        {
            var filelog = string.Empty;
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                filelog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assembly.Location), "log_transaction.txt");
            }
            else
            {
                filelog = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "log_transaction.txt");
            }

            var timeout = DateTime.Now.AddMinutes(5);
            while (CompleteThreadStarted)
            {
                try
                {
                    #region write log
                    if (!string.IsNullOrEmpty(filelog) && timeout < DateTime.Now)
                    {
                        try
                        {
                            timeout = DateTime.Now.AddMinutes(5);
                            var sb = new StringBuilder();
                            foreach (var item in Transactions)
                            {
                                if (item.Value == null || item.Value.IsEmpty)
                                    continue;

                                sb.AppendFormat("{0:-12} {1} / {2}", item.Key, item.Value.Count, item.Value.SelectMany(p => p.values).Count());
                                sb.AppendLine();
                            }

                            if (sb.Length > 0)
                            {
                                System.IO.File.WriteAllText(filelog, sb.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            ConfigurationClient.WindowsLog("CompleteTransactions", string.Empty, "system", ex.ToString());
                        }
                    }
                    #endregion

                    ComitTransactions();
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog("CompleteTransactions", string.Empty, "system", ex.ToString());
                }

                Thread.Sleep(200);
            }
        }
        public static void ComitTransactions()
        {
            //var option = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
            //Parallel.ForEach(Transactions.Values.ToArray(), option, domain =>
            foreach (var domain in Transactions.Values.ToArray())
            {
                try
                {
                    if (domain == null)
                        return;

                    var i = 0; //записывать за раз не более 100 значений
                    while (domain.Count > 0 && i++ < 10)
                    {
                        MemoryTransaction transaction;
                        if (domain.TryDequeue(out transaction))
                        {
                            for (int tr = 0; tr < 5; tr++)
                            {
                                try
                                {
                                    new DataClient().СохранитьЗначение(transaction.id_node, transaction.values,
                                        false, transaction.IsNew,
                                        transaction.stage, transaction.user, transaction.domain,
                                        false);

                                    break;
                                }
                                catch (SqlException ex)
                                {
                                    ConfigurationClient.WindowsLog(ex.Message, "ComitTransactions", transaction.domain, ex.ToString());
                                    System.Threading.Thread.Sleep(1500);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog("CompleteTransactions/Item", string.Empty, "system", ex.ToString());
                }
            } //);
        }
    }

    //[DataContract, Serializable]
    internal class MemoryTransaction
    {
        public decimal id_node;
        public Dictionary<string, Value> values;
        //public bool ДобавитьВИсторию { get; set; }
        public bool IsNew;
        public Хранилище stage;
        public string user;
        public string domain;
    };


    [DataContract, Serializable]
    public class ValueContanier
    {
        [DataMember]
        public DateTime cache;
        [DataMember]
        public Value obj;
    }
}

