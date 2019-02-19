using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ServiceStack.Redis;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace RosService.Caching
{
    public class ClientRedis : IDisposable
    {
        public static readonly List<ServiceStack.Redis.PooledRedisClientManager> Clients;
        private static List<string> _Hosts;
        public static List<string> Hosts
        {
            get
            {
                if (_Hosts == null)
                {
                    _Hosts = new List<string>();
                    if (System.Configuration.ConfigurationManager.AppSettings["MemoryCache"] != null)
                    {
                        foreach (var item in System.Configuration.ConfigurationManager.AppSettings["MemoryCache"].Split(';'))
                        {
                            if (string.IsNullOrEmpty(item))
                                continue;

                            _Hosts.Add(item);
                        }
                    }
                }
                return _Hosts;
            }
        }
        public static int Db
        {
            get
            {
                var db = 0;
                int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MemoryCache.Db"], out db);
                return db;
            }
        }
        public static int PoolSize
        {
            get
            {
                var PoolSize = 0;
                int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MemoryCache.PoolSize"], out PoolSize);
                if (PoolSize <= 0)
                    PoolSize = 20;
                return PoolSize;
            }
        }

        static ClientRedis()
        {
            if (Hosts != null && Hosts.Count > 0 && Clients == null)
            {
                var config = new ServiceStack.Redis.RedisClientManagerConfig()
                {
                    MaxReadPoolSize = PoolSize,
                    MaxWritePoolSize = PoolSize,
                    DefaultDb = Db
                };

                Clients = new List<PooledRedisClientManager>();
                foreach (var item in Hosts)
                {
                    var connect = new string[] { item, item, item };
                    Clients.Add(new ServiceStack.Redis.PooledRedisClientManager(connect, connect, config));
                }
            }
        }

        public static PooledRedisClientManager GetClient(decimal key)
        {
            var guid = BitConverter.GetBytes(Convert.ToInt64(key));
            var index = Convert.ToInt32(new crc32().CRC(guid) % Hosts.Count);
            return Clients[index];
        }
        public static PooledRedisClientManager GetClient(string key)
        {
            var index = Convert.ToInt32(new crc32().CRC(key) % Hosts.Count);
            return Clients[index];
        }

        public static ConcurrentDictionary<string, object> Get(IEnumerable<string> keys)
        {
            var result = new ConcurrentDictionary<string, object>();
            var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount / 2 };
            var groups = keys.GroupBy(z => Convert.ToInt32(new crc32().CRC(z) % Hosts.Count));
            Parallel.ForEach(groups, options, i =>
            {
                var pool = Clients[i.Key];
                var client = null as IRedisClient;
                try
                {
                    client = pool.GetReadOnlyClient();
                    if (client != null)
                    {
                        foreach (var item in client.GetAll<string>(i))
                        {
                            result.TryAdd(item.Key, ConverGetValue(item.Value));
                        }
                    }
                }
                finally
                {
                    if (pool != null && client != null)
                        pool.DisposeReadOnlyClient(client as RedisNativeClient);
                }
            });
            return result;
        }
        public static object Get(string key)
        {
            //var value = GetClient(key).Get<string>(key);

            var value = null as string;
            var pool = GetClient(key);
            var client = null as IRedisClient;
            try
            {
                client = pool.GetReadOnlyClient();
                if (client != null)
                    value = client.Get<string>(key);
            }
            finally
            {
                if (pool != null && client != null)
                    pool.DisposeReadOnlyClient(client as RedisNativeClient);
            }


            // Если null то его и возвращаем - это значит что значения нет в кеше
            if (value == null)
                return null;

            return ConverGetValue(value);
        }
        public static void Set(string key, object value)
        {
            var pool = GetClient(key);
            var client = null as IRedisClient;
            try
            {
                client = pool.GetClient();

                if (client != null)
                    client.Set<string>(key, ConvertSetValue(value));
            }
            finally
            {
                if (pool != null && client != null)
                    pool.DisposeClient(client as RedisNativeClient);
            }
            //GetClient(key).Set<string>(key, ConvertSetValue(value));
        }
        public static void Set(string key, object value, DateTime timeout)
        {
            var pool = GetClient(key);
            var client = null as IRedisClient;
            try
            {
                client = pool.GetClient();
                if (client != null)
                    client.Set<string>(key, ConvertSetValue(value), timeout);
            }
            finally
            {
                if (pool != null && client != null)
                    pool.DisposeClient(client as RedisNativeClient);
            }

            //GetClient(key).Set<string>(key, ConvertSetValue(value), timeout);
        }

        protected static object ConverGetValue(string value)
        {
            // Проблема Redis string.Empty возвращает как null
            if (value == null)
                return null;

            if ("(NULL)".Equals(value))
                return Convert.DBNull;

            var str = Convert.ToString(value);

            switch (str[0])
            {
                case 'S':
                    return str.Remove(0, 1);
                case 'M':
                    return Convert.ToDecimal(str.Remove(0, 1));
                case 'F':
                    return Convert.ToSingle(str.Remove(0, 1));
                case 'I':
                    return Convert.ToInt32(str.Remove(0, 1));
                case 'L':
                    return Convert.ToInt64(str.Remove(0, 1));
                case 'B':
                    return Convert.ToBoolean(str.Remove(0, 1));
                case 'D':
                    return Convert.ToDateTime(str.Remove(0, 1));
                case 'O':
                    return System.Text.Encoding.Default.GetBytes(str.Remove(0, 1));
            }

            return value;
        }
        protected static string ConvertSetValue(object value)
        {
            var setValue = string.Empty;

            if (value == null)
            {
                setValue = "(NULL)";
            }
            else if (value is string)
            {
                setValue = "S" + value;
            }
            else if (value is decimal)
            {
                setValue = "M" + value;
            }
            else if (value is float)
            {
                setValue = "F" + value;
            }
            else if (value is int)
            {
                setValue = "I" + value;
            }
            else if (value is long)
            {
                setValue = "L" + value;
            }
            else if (value is bool)
            {
                setValue = "B" + value;
            }
            else if (value is DateTime)
            {
                setValue = "D" + value;
            }
            else if (value is byte[])
            {
                setValue = "O" + System.Text.Encoding.Default.GetString((byte[])value);
            }
            else
            {
                setValue = "(NULL)";
            }
            return setValue;
        }

        public void Dispose()
        {
        }
    }
}