using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace RosService.Security
{
    public static class Security
    {
        //кеш баз данных
        private static ConcurrentDictionary<string, Db> items = new ConcurrentDictionary<string, Db>();

        /// <summary>
        /// Проверить лецензию базы данных
        /// </summary>
        /// <param name="db">Имя базы данных</param>
        /// <param name="result">Сообщение об ошибке</param>
        /// <returns>true если ошибок нет</returns>
        public static bool ValidDb(string db, out string result)
        {
            try
            {
                using (var web = new System.Net.WebClient())
                {
                    var key = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(db));
                    var url = @"http://api.itrf.ru/valid/" + key;
                    var response = System.Text.Encoding.UTF8.GetString(web.DownloadData(url));
                    if (response.StartsWith("OK"))
                    {
                        result = string.Empty;
                        return true;
                    }
                    else if (response.StartsWith("Error"))
                    {
                        result = (response.Split(':').ElementAtOrDefault(1) ?? string.Empty).Trim();
                    }
                    else
                    {
                        result = response;
                    }
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return false;
        }

        /// <summary>
        /// Проверка базы данных раз в 24 часа
        /// </summary>
        /// <param name="db">Имя базы данных</param>
        /// <returns></returns>
        public static string ValidDbAsync(string db)
        {
            var item = items.GetOrAdd(db, new Db());
            if (item.checktime < DateTime.Now)
            {
                item.checktime = DateTime.Now.AddHours(24);
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    ValidDb(db, out item.result);
                });
            }
            return item.result;
        }
    }

    internal class Db
    {
        public DateTime checktime;
        public string result;
    }
}
