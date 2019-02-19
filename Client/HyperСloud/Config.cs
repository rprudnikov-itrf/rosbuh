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
using RosService.Configuration;

namespace HyperСloud
{
    public class Config : IDisposable
    {
        private static readonly object lockobj = new object();
        private static Dictionary<string, PooledClientManager<IConfiguration>> pooledClientManager = new Dictionary<string, PooledClientManager<IConfiguration>>();
        private PooledClientManager<IConfiguration> CurrentPool { get; set; }

        public string Db { get; private set; }

        public Config()
        {
            Initialize(System.Configuration.ConfigurationManager.AppSettings["RosService.Db"] ?? RosService.Client.Domain);
        }
        public Config(string db)
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
                    CurrentPool = new PooledClientManager<IConfiguration>(10, Host, "Configuration");
                    pooledClientManager.Add(Host, CurrentPool);
                }
            }
        }

        /// <summary>
        /// Получить список данных
        /// </summary>
        /// <returns></returns>
        public RosService.Configuration.Type[] GetTypes()
        {
            return CurrentPool.GetClient().Channel.СписокТипов(new string[0], Db);
        }

        /// <summary>
        /// Копировать тип данных
        /// </summary>
        /// <param name="ТипДанных"></param>
        /// <param name="ИзДомена"></param>
        /// <param name="КопироватьВ"></param>
        /// <param name="КопироватьВДомен"></param>
        /// <param name="УсловияКопирования"></param>
        public void CopyType(string ТипДанных, string ИзДомена, string КопироватьВ, string КопироватьВДомен, УсловияКопирования УсловияКопирования)
        {
            CurrentPool.GetClient().Channel.КопироватьТипДанных(ТипДанных, ИзДомена, КопироватьВ, КопироватьВДомен, УсловияКопирования, string.Empty, Db);
        }

        public void AddType(string Name, string Description, string Namespace, string BaseType)
        {
            CurrentPool.GetClient().Channel.ДобавитьТип(0, Name, Description, Namespace, BaseType, false, true, string.Empty, Db);
        }
        public void AddAttribute(string Type, string Attribute)
        {
            CurrentPool.GetClient().Channel.ДобавитьАтрибут(Type, Attribute, true, string.Empty, Db);
        }

        public void AddJornal(string Message, string StackTrace)
        {
            CurrentPool.GetClient().Channel.ЖурналСобытийДобавитьОшибку(Message, StackTrace, string.Empty, Db);
        }

        public void Dispose()
        {
            CurrentPool = null;
        }
    }
}
