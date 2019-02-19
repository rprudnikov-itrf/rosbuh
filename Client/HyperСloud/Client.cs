using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using RosService.Data;
using System.ServiceModel.Channels;
using RosService.ServiceModel;
using System.Collections.Concurrent;
using System.Threading;

namespace HyperСloud
{
    public class Client : IDisposable
    {
        private static readonly object lockobj = new object();
        private static Dictionary<string, PooledClientManager<IData>> pooledClientManager = new Dictionary<string,PooledClientManager<IData>>();
        private PooledClientManager<IData> CurrentPool { get; set; }

        public string Db { get; private set; }

        public Client()
        {
            Initialize(System.Configuration.ConfigurationManager.AppSettings["RosService.Db"] ?? RosService.Client.Domain);
        }
        public Client(string db)
        {
            Initialize(db);
        }

        private void Initialize(string db)
        {
            lock (lockobj)
            {
                var url = (db ?? string.Empty).Split('@');
                var Host = url.ElementAtOrDefault(1) ?? string.Empty;
                Db = url.ElementAtOrDefault(0);

                if (pooledClientManager.ContainsKey(Host))
                {
                    CurrentPool = pooledClientManager[Host];   
                }
                else
                {
                    CurrentPool = new PooledClientManager<IData>(10, Host, "Data");
                    pooledClientManager.Add(Host, CurrentPool);
                }
            }
        }


        public T Get<T>(object id, string key)
        {
            try
            {
                var value = CurrentPool.GetClient().Channel.ПолучитьЗначениеПростое(id, key, Хранилище.Оперативное, Db);
                if (value is T)
                {
                    return (T)value;
                }
                else if (value == null && typeof(T) == typeof(DateTime))
                {
                    return (T)(object)DateTime.MinValue;
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
        public Dictionary<string, object> Get(object id, params string[] keys)
        {
            return RosService.Helper.ConvertDataValue(CurrentPool.GetClient().Channel.ПолучитьЗначение(id, keys, Хранилище.Оперативное, Db));
        }


        public void Set(object id, Dictionary<string, object> values)
        {
            CurrentPool.GetClient().Channel.СохранитьЗначение(id, RosService.Helper.ConvertDataValue(values), false, Хранилище.Оперативное, string.Empty, Db);
        }
        public void Set(object id, string key, object value)
        {
            CurrentPool.GetClient().Channel.СохранитьЗначениеПростое(id, key, value, false, Хранилище.Оперативное, string.Empty, Db);
        }
        public void SetAsync(object id, Dictionary<string, object> values)
        {
            CurrentPool.GetClient().Channel.СохранитьЗначениеАсинхронно(id, RosService.Helper.ConvertDataValue(values), false, Хранилище.Оперативное, string.Empty, Db);
        }
        public void SetAsync(object id, string key, object value)
        {
            var values = new Dictionary<string, Value>();
            values.Add(key, new Value() { Значение = value });
            CurrentPool.GetClient().Channel.СохранитьЗначениеАсинхронно(id, values, false, Хранилище.Оперативное, string.Empty, Db);
        }

        public void Delete(decimal id)
        {
            CurrentPool.GetClient().Channel.УдалитьРаздел(false, false, new decimal[] { id }, Хранилище.Оперативное, string.Empty, Db);
        }
        public void Delete(decimal[] id)
        {
            CurrentPool.GetClient().Channel.УдалитьРаздел(false, false, id, Хранилище.Оперативное, string.Empty, Db);
        }
        public void DeleteAsync(decimal id)
        {
            CurrentPool.GetClient().Channel.УдалитьРазделАсинхронно(false, false, new decimal[] { id }, Хранилище.Оперативное, string.Empty, Db);
        }
        public void DeleteAsync(decimal[] id)
        {
            CurrentPool.GetClient().Channel.УдалитьРазделАсинхронно(false, false, id, Хранилище.Оперативное, string.Empty, Db);
        }

        public decimal Add(object id, string type, Dictionary<string, object> values)
        {
            return CurrentPool.GetClient().Channel.ДобавитьРаздел(id, type, RosService.Helper.ConvertDataValue(values), false, Хранилище.Оперативное, string.Empty, Db);
        }
        public decimal AddAsync(object id, string type, Dictionary<string, object> values)
        {
            return CurrentPool.GetClient().Channel.ДобавитьРазделАсинхронно(id, type, RosService.Helper.ConvertDataValue(values), false, Хранилище.Оперативное, string.Empty, Db);
        }

        public TableValue Search(RosService.Data.Query query)
        {
            return CurrentPool.GetClient().Channel.Поиск(query, Хранилище.Оперативное, Db);
        }
        public TableValue Search(string query)
        {
            return CurrentPool.GetClient().Channel.Поиск(new Query(query), Хранилище.Оперативное, Db);
        }

        public T GetMemCache<T>(string key)
        {
            try
            {
                var value = CurrentPool.GetClient().Channel.ПолучитьКешЗначение(key, Db);
                if (value is T)
                {
                    return (T)value;
                }
                else if (value == null && typeof(T) == typeof(DateTime))
                {
                    return (T)(object)DateTime.MinValue;
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
        public void SetMemCache<T>(string key, object value, int timeout)
        {
            CurrentPool.GetClient().Channel.СохранитьКешЗначение(key, value, timeout, Db);
        }

        public void UpdateMemCache(string cachename, decimal id, Dictionary<string, object> values)
        {
            CurrentPool.GetClient().Channel.ОбновитьЗначениеВКеше(cachename, new decimal[] { id }, values, string.Empty, Db);
        }
        public void UpdateMemCache(string cachename, decimal[] id, Dictionary<string, object> values)
        {
            CurrentPool.GetClient().Channel.ОбновитьЗначениеВКеше(cachename, id, values, string.Empty, Db);
        }

        public void Dispose()
        {
            CurrentPool = null;
        }
    }
}
