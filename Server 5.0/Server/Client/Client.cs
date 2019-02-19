using System;
using System.ServiceModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using RosService.Users;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using RosService.ServiceModel;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Threading;
using RosService.Data;
using RosService.Configuration;
using RosService.Services;
using RosService.Finance;
using RosService.Files;

namespace RosService
{
    public class Client : IDisposable
    {
        public static object lockobj = new System.Object();
        public static bool IsAuthorization { get; private set; }
        public static string UserName { get; private set; }
        public static string Password { get; private set; }
        public static string Domain { get; private set; }
        public static UserClient User { get; private set; }

        #region event
        public static EventHandler<AddClientEventArgs> ПередДобавлением;
        public static EventHandler<AddClientPostEventArgs> ПослеДобавления;
        #endregion

        public string Домен
        {
            get
            {
                return Domain;
            }
        }
        public string Пользователь
        {
            get
            {
                return UserName;
            }
        }
        public UserClient СведенияПользователя
        {
            get
            {
                return User;
            }
        }


        public DataClient Архив { get; private set; }
        public ConfigurationClient Конфигуратор { get; private set; }
        public FinanceClient Бухгалтерия { get; private set; }
        public ServicesClient Сервисы { get; private set; }
        public FileClient Файлы { get; private set; }
        
        static Client()
        {
            User = new UserClient() { Права = new НаборПраваПользователя() };
            UserName = "";
            Password = "";
            Domain = "";
        }
        public Client()
        {
            InitializeComponent();

            Архив = new DataClient();
            Конфигуратор = new ConfigurationClient();
            Бухгалтерия = new FinanceClient();
            Сервисы = new ServicesClient();
            Файлы = new FileClient();
        }
        public Client(TimeSpan timeout)
        {
            InitializeComponent();

            Архив = new DataClient();
            Конфигуратор = new ConfigurationClient();
            Бухгалтерия = new FinanceClient();
            Сервисы = new ServicesClient();
            Файлы = new FileClient();
        }
        public void Dispose()
        {
            if (Архив != null) Архив = null;
            if (Конфигуратор != null) Конфигуратор = null;
            if (Бухгалтерия != null) Бухгалтерия = null;
            if (Сервисы != null) Сервисы = null;
            if (Файлы != null) Файлы = null;
        }

        protected void InitializeComponent()
        {
            if (!IsAuthorization)
            {
                lock (lockobj)
                {
                    //Произвести авторизацию из web.config
                    if (!IsAuthorization)
                    {
                        Authorization(
                            System.Configuration.ConfigurationManager.AppSettings["RosService.UserName"],
                            System.Configuration.ConfigurationManager.AppSettings["RosService.Password"],
                            false);
                    }
                }
            }
        }

        #region Авторизация
        public static bool Authorization(string UserName, string Password, bool ЗаписатьВЖурнал)
        {
            if (UserName == null)
                return false;
            else if (string.IsNullOrEmpty(UserName.Trim())) 
                throw new Exception("Заполните поле имя пользователя");

            try
            {
                var token = Regex.Match(UserName, @"^(?<DOMAIN>(.+?))[\\/](?<USER>(.+?))(@(?<HOST>(.+)))?$", RegexOptions.Compiled | RegexOptions.Singleline);
                Client.Domain = token.Groups["DOMAIN"].Value.ToLower();
                Client.UserName = token.Groups["USER"].Value;
                Client.Password = Password;
                //if (!string.IsNullOrEmpty(token.Groups["HOST"].Value))
                //{
                //    Client.Host = token.Groups["HOST"].Value;
                //}
                if (string.IsNullOrEmpty(Client.UserName)) throw new ArgumentException();
            }
            catch(ArgumentException)
            {
                throw new Exception(@"Не правильно указано имя пользователя, используйте формат 'компания\Пользователь'.");
            }

            //проверить существует ли пользователь
            try
            {
                var user = new ConfigurationClient().Авторизация(Client.UserName, Client.Password, ЗаписатьВЖурнал, Client.Domain);
                Client.UserName = user.Логин;
                Client.User.id_node = user.id_node;
                Client.User.Тип = user.Тип;
                Client.User.Роли = user.Роли;
                Client.User.ГруппаРаздел = user.ГруппаРаздел;
                Client.User.Группа = user.Группа;
                Client.User.Права.НаборПравДоступа = user.Права;
                Client.User.МестоПоиска = user.МестоПоиска;
                Client.User.ПоисковыйАтрибут = user.ПоисковыйАтрибут;
            }
            catch (NullReferenceException)
            {
                throw new Exception("Не верно указано имя пользователя или пароль.");
            }
            //catch (EndpointNotFoundException)
            //{
            //    throw new Exception(string.Format("Не удалось установить связь с сервером ({0}). Проверьте подключение к Интернет.", host));
            //}
            //catch (TimeoutException)
            //{
            //    throw new Exception(string.Format("Не удалось установить связь с сервером ({0}). Проверьте подключение к Интернет.", host));
            //}
            Client.IsAuthorization = true;
            return true;
        }
        public static void Shutdown()
        {
            if (IsAuthorization)
            {
                Domain = UserName = Password = "";
                IsAuthorization = false;
            }
        }
        public static void ПродлитьСессиюПользователя(TimeSpan ВремяСесси)
        {
            if (!IsAuthorization) return;

            new ConfigurationClient().АвторизацияПродлитьСессиюПользователя(User.id_node, ВремяСесси, Domain);
        }
        #endregion
    }

    public class AddClientEventArgs : EventArgs
    {
        public object id_parent { get; private set; }
        public string Тип { get; private set; }
        public Dictionary<string, object> Значения { get; private set; }


        public AddClientEventArgs()
        {
        }
        public AddClientEventArgs(object parent, string type, Dictionary<string, object> value)
        {
            id_parent = parent;
            Тип = type;
            Значения = value;
        }
    }
    public class AddClientPostEventArgs : AddClientEventArgs
    {
        public object id_node { get; private set; }

        public AddClientPostEventArgs()
        {
        }
        public AddClientPostEventArgs(decimal node, object parent, string type, Dictionary<string, object> value)
            : base(parent, type, value)
        {
            id_node = node;
        }
    }
}
