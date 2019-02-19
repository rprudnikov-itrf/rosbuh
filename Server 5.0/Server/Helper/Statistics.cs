using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RosService.Helper
{
    public class Statistics
    {
        //public static long Get;
        //public static long Set;
        //public static long Search;
        //public static long Delete;


        //private static object lockSend = new System.Object();
        //private static System.Timers.Timer run;

        public static void Run()
        {
            //lock (lockSend)
            //{
            //    run = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
            //    run.AutoReset = true;
            //    run.Elapsed += new System.Timers.ElapsedEventHandler(run_Elapsed);
            //    run.Start();
            //}
        }

        private static void run_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //lock (lockSend)
            //{
            //    var Services = new RosService.Services.ServicesClient();
            //    var Server = Environment.MachineName;
            //    Services.ОптравитьВСтатистику("HyperCloud5.Get", Server, DateTime.Now, Get);
            //    Services.ОптравитьВСтатистику("HyperCloud5.Set", Server, DateTime.Now, Set);
            //    Services.ОптравитьВСтатистику("HyperCloud5.Search", Server, DateTime.Now, Search);
            //    Services.ОптравитьВСтатистику("HyperCloud5.Delete", Server, DateTime.Now, Delete);

            //    Get = 0;
            //    Set = 0;
            //    Search = 0;
            //    Delete = 0;
            //}
        }
    }
}
