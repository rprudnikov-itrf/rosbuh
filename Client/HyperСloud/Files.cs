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
using RosService.Files;

namespace HyperСloud
{
    public class Files : IDisposable
    {
        private static readonly object lockobj = new object();
        private static Dictionary<string, PooledClientManager<IFile>> pooledClientManager = new Dictionary<string, PooledClientManager<IFile>>();
        private static Dictionary<string, PooledClientManager<IData>> pooledClientManagerData = new Dictionary<string, PooledClientManager<IData>>();
        private PooledClientManager<IFile> CurrentPool { get; set; }
        private PooledClientManager<IData> CurrentPoolData { get; set; }

        public string Db { get; private set; }

        public Files()
        {
            Initialize(System.Configuration.ConfigurationManager.AppSettings["RosService.Db"] ?? RosService.Client.Domain);
        }
        public Files(string db)
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
                    CurrentPool = new PooledClientManager<IFile>(10, Host, "File");
                    pooledClientManager.Add(Host, CurrentPool);
                }

                if (pooledClientManagerData.ContainsKey(Host))
                {
                    CurrentPoolData = pooledClientManagerData[Host];
                }
                else
                {
                    CurrentPoolData = new PooledClientManager<IData>(3, Host, "Data");
                    pooledClientManagerData.Add(Host, CurrentPoolData);
                }
            }
        }

        public void Dispose()
        {
            CurrentPool = null;
            CurrentPoolData = null;
        }

        /// <summary>
        /// Получить список файлов в разделе
        /// </summary>
        /// <param name="id">Номер раздела</param>
        public IEnumerable<RosService.Data.ФайлИнформация> GetFiles(object id)
        {
            return CurrentPoolData.GetClient().Channel.СписокФайлов(Convert.ToDecimal(id), RosService.Data.Хранилище.Оперативное, Db);
        }

        /// <summary>
        /// Получить количество файлов в разделе
        /// </summary>
        /// <param name="id">Номер раздела</param>
        /// <returns></returns>
        public int Count(object id)
        {
            return CurrentPoolData.GetClient().Channel.КоличествоФайлов(Convert.ToDecimal(id), RosService.Data.Хранилище.Оперативное, Db);
        }


        public byte[] Get(object id)
        {
            var file = CurrentPool.GetClient().Channel.ПолучитьФайлПолностью(new ПолучитьФайлПолностьюRequest(id, RosService.Files.Хранилище.Оперативное, Db));
            if (file != null)
                return file.ПолучитьФайлПолностьюResult;

            return new byte[0];
        }

        public decimal Set(object id, string filename, byte[] stream)
        {
            return Set(id, filename, stream, string.Empty);
        }
        public decimal Set(object id, string filename, byte[] stream, string ИдентификаторОбъекта)
        {
            RosService.Files.СохранитьФайлПолностьюRequest inValue = new RosService.Files.СохранитьФайлПолностьюRequest();
            inValue.id_node = id;
            inValue.ИдентификаторФайла = ИдентификаторОбъекта;
            inValue.ИмяФайла = filename;
            inValue.Описание = string.Empty;
            inValue.stream = stream;
            inValue.хранилище = RosService.Files.Хранилище.Оперативное;
            inValue.user = String.Empty;
            inValue.domain = Db;

            RosService.Files.СохранитьФайлПолностьюResponse retVal = CurrentPool.GetClient().Channel.СохранитьФайлПолностью(inValue);
            return retVal.СохранитьФайлПолностьюResult;
            
            //var file = CurrentPool.GetClient().Channel.СохранитьФайлПолностью(new СохранитьФайлПолностьюRequest(id, ИдентификаторОбъекта, filename, string.Empty, stream, RosService.Files.Хранилище.Оперативное, "", Db));
            //if (file != null)
            //    return file.СохранитьФайлПолностьюResult;
            //return 0;
        }


        public void SetAsync(object id, string filename, byte[] stream)
        {
            SetAsync(id, filename, stream, string.Empty);
        }
        public void SetAsync(object id, string filename, byte[] stream, string ИдентификаторОбъекта)
        {
            CurrentPool.GetClient().Channel.СохранитьФайлПолностьюАсинхронно(new СохранитьФайлПолностьюАсинхронно(id, ИдентификаторОбъекта, filename, string.Empty, stream, RosService.Files.Хранилище.Оперативное, "", Db));
        }
    }
}
