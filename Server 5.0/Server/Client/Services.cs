using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RosService.Services
{
    public partial class ServicesClient
    {
        public void Рассылка(string Тема, string Содержание, object ПапкаАдресатами, Dictionary<string, byte[]> Файлы)
        {
            Рассылка(Тема, Содержание, ПапкаАдресатами, Файлы, Client.UserName, Client.Domain);
        }
        public void Рассылка(string Тема, string Содержание, object ПапкаАдресатами)
        {
            Рассылка(Тема, Содержание, ПапкаАдресатами, null, Client.UserName, Client.Domain);
        }
        public void Рассылка(string Тема, string Содержание)
        {
            Рассылка(Тема, Содержание, 0, null, Client.UserName, Client.Domain);
        }

        public void СообщенияПользователя_Добавить(object ОтКогоЛогинПользователя, object КомуЛогинПользователя, string Сообщение, string user, string domain)
        {
            СообщенияПользователя_Добавить(ОтКогоЛогинПользователя, new object[] { КомуЛогинПользователя }, Сообщение, user, domain);
        }
        public void СообщенияПользователя_Добавить(object ОтКогоЛогинПользователя, object КомуЛогинПользователя, string Сообщение)
        {
            СообщенияПользователя_Добавить(ОтКогоЛогинПользователя, new object[] { КомуЛогинПользователя }, Сообщение, RosService.Client.UserName, RosService.Client.Domain);
        }
        public void СообщенияПользователя_Добавить(object КомуЛогинПользователя, string Сообщение)
        {
            СообщенияПользователя_Добавить(RosService.Client.UserName, new object[] { КомуЛогинПользователя }, Сообщение, RosService.Client.UserName, RosService.Client.Domain);
        }
    }
}