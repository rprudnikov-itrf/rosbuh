using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.ServiceModel;
using System.Threading;
using RosService.Intreface;
using System.Runtime.Remoting;
using RosService.Data;
using RosService.Configuration;
using RosService.Services;
using RosService.Finance;
using RosService.Files;
using RosService.Caching;

namespace RosService
{
    public partial class RosServiceHost : ServiceBase
    {
        private List<System.Timers.Timer> timers;
        public List<ServiceHost> hosts;
        
        public RosServiceHost()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            hosts = new List<ServiceHost>();
            timers = new List<System.Timers.Timer>();

            Caching.Cache.Загрузить(GetCacheDirectory());
            //Caching.Cache.RegisterMemoryCacheService();

            #region wcf
            AddHost(new ServiceHost(typeof(DataClient)));
            AddHost(new ServiceHost(typeof(ConfigurationClient)));
            AddHost(new ServiceHost(typeof(FinanceClient)));
            AddHost(new ServiceHost(typeof(ServicesClient)));
            AddHost(new ServiceHost(typeof(FileClient)));
            //AddHost(new ServiceHost(typeof(CrossDomain)));

            foreach (var item in hosts)
            {
                if (item == null) continue;
                item.Open();
            }
            #endregion

            #region Сохранять накопленные значения
            //var historyTimer = new System.Timers.Timer(TimeSpan.FromMinutes(60).TotalMilliseconds);
            //historyTimer.AutoReset = true;
            //historyTimer.Elapsed += new System.Timers.ElapsedEventHandler(historyTimer_Elapsed);
            //historyTimer.Start();
            //timers.Add(historyTimer);
            #endregion

            #region Очистить не используемые значения
            var valueTimer = new System.Timers.Timer(TimeSpan.FromHours(3).TotalMilliseconds);
            valueTimer.AutoReset = true;
            valueTimer.Elapsed += new System.Timers.ElapsedEventHandler(valueTimer_Elapsed);
            valueTimer.Start();
            timers.Add(valueTimer);
            #endregion

            #region Очистить журналы
            //System.Threading.Tasks.Task.Factory.StartNew(() =>
            //{
            //    Thread.Sleep(TimeSpan.FromMinutes(1));
            //    try
            //    {
            //        foreach (var domain in new ConfigurationClient().СписокДоменов())
            //        {
            //            #region очистить журналы
            //            try
            //            {
            //                var query = new Query();
            //                query.ДобавитьТипы("Exception");
            //                query.ДобавитьУсловиеПоиска("ДатаСозданияОбъекта", DateTime.Now.AddDays(-5), Query.Оператор.МеньшеРавно);
            //                new DataClient().УдалитьРазделПоиск(false, false, query, Хранилище.Оперативное, Program.SERVICE_NAME, domain);
            //            }
            //            catch (Exception ex)
            //            {
            //                ExceptionEventLog(ex);
            //            }
            //            #endregion
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        ExceptionEventLog(ex);
            //    }
            //});
            #endregion

            #region Сохранить кеш в файл
            //var cacheTimer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds);
            //cacheTimer.AutoReset = true;
            //cacheTimer.Elapsed += new System.Timers.ElapsedEventHandler(cacheTimer_Elapsed);
            //cacheTimer.Start();
            //timers.Add(cacheTimer);
            #endregion

            #region АрхивироватьФайл
            //var cacheTimerАрхивироватьФайл = new System.Timers.Timer(TimeSpan.FromDays(1).TotalMilliseconds);
            //cacheTimerАрхивироватьФайл.AutoReset = true;
            //cacheTimerАрхивироватьФайл.Elapsed += new System.Timers.ElapsedEventHandler(cacheTimer_Elapsed);
            //cacheTimerАрхивироватьФайл.Start();
            //timers.Add(cacheTimerАрхивироватьФайл);
            #endregion

            //статистика
            RosService.Helper.Statistics.Run();
        }

        protected override void OnStop()
        {
            Caching.Cache.Сохранить(GetCacheDirectory(), true, false);

            foreach (var item in timers)
            {
                if (item == null) 
                    continue;
                item.Stop();
                item.Dispose();
            }
            timers.Clear();
            timers = null;

            foreach (var item in hosts)
            {
                if (item == null) 
                    continue;
                item.Abort();
            }
            hosts.Clear();
            hosts = null;

            try
            {
                if (MemoryCache.CompleteThread != null && MemoryCache.CompleteThreadStarted)
                {
                    MemoryCache.CompleteThreadStarted = false;
                    MemoryCache.CompleteThread.Join(5 * 1000);
                }
            }
            catch(Exception ex)
            {
                ExceptionEventLog(ex);
            }
        }

        private void AddHost(ServiceHost serviceHost)
        {
            try
            {
                hosts.Add(serviceHost);
            }
            catch (Exception ex)
            {
                ExceptionEventLog(ex);
            }
        }
        //private void memoryCleanTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    try
        //    {
        //        GC.Collect();
        //        GC.WaitForPendingFinalizers();
        //        GC.Collect();
        //    }
        //    catch (Exception ex)
        //    {
        //        ExceptionEventLog(ex);
        //    }
        //}
        private void valueTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var timer = sender as System.Timers.Timer;

            try
            {
                if (timer != null) 
                    timer.Stop();

                RosService.Caching.MemoryCache.Clear();
                RosService.Caching.Cache.Clear();
            }
            catch (Exception ex)
            {
                ExceptionEventLog(ex);
            }
            finally
            {
                if (timer != null) 
                    timer.Start();
            }
        }
        //private void historyTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    var timer = sender as System.Timers.Timer;
        //    if (timer != null) timer.Stop();

        //    RosService.Caching.Cache.SaveHistoryValues();

        //    if (timer != null) timer.Start();
        //}
        //private void cacheTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    var timer = sender as System.Timers.Timer;

        //    try
        //    {
        //        if (timer != null) 
        //            timer.Stop();

        //        Caching.Cache.Сохранить(GetCacheDirectory(), false, true);
        //    }
        //    catch (Exception ex)
        //    {
        //        ExceptionEventLog(ex);
        //    }
        //    finally
        //    {
        //        if (timer != null)
        //            timer.Start();
        //    }
        //}
        //private void cacheTimerАрхивироватьФайл_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    var timer = sender as System.Timers.Timer;
        //    if (timer != null) timer.Stop();

        //    new RosService.ServerClient().АрхивироватьФайлы();

        //    if (timer != null) timer.Start();
        //}
        private void ExceptionEventLog(Exception ex)
        {
            try
            {
                //Записать в Windows-log
                if (!EventLog.SourceExists(Program.SERVICE_DISPLAY))
                {
                    EventLog.CreateEventSource(Program.SERVICE_DISPLAY, Program.SERVICE_DISPLAY);
                }
                EventLog.WriteEntry(Program.SERVICE_DISPLAY, ex.ToString(), System.Diagnostics.EventLogEntryType.Error, 1, 1);
            }
            catch(Exception)
            {
            }
        }

        private string GetCacheDirectory()
        {
            //return System.Reflection.Assembly.GetEntryAssembly().Location + ".cache";
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "cache");
        }
    }
}
