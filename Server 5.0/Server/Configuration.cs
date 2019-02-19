using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Transactions;
using System.Xml;
using Microsoft.CSharp;
using RosService.DataClasses;
using RosService.Intreface;
using System.Text.RegularExpressions;
using RosService.Caching;
using RosService.Helper;
using System.Configuration;
using System.Threading.Tasks;
using RosService.Data;


namespace RosService.Configuration
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    AddressFilterMode = AddressFilterMode.Any,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false,
    ConfigurationName = "RosService.Configuration")]
    public partial class ConfigurationClient : IConfiguration
    {
        public enum СистемныеПапки
        {
            РазделТипы = 2447,
            РазделЗначения = 320,
            ПравилаКаталогизации = 968,
            ПривязкиКФормам = 661,
            СобытияКФормам = 1059,
            //Пользователи = 673
        }

        #region Работа с ключами
        /// <summary>
        /// Сформировать ключ длинной 5 символов из числа
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetHashKey(decimal id)
        {
            if (id == 0) return string.Empty;
            string hash = string.Format("{0:x5}", Convert.ToInt32(id));
            if (hash.Length > 5) return hash.Substring(hash.Length - 5, 5);
            return hash;
        }
        /// <summary>
        /// Сформировать ключ для поиска в глубину
        /// </summary>
        /// <param name="id"></param>
        /// <param name="idHashKey"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static string GetHashKeyLevel(decimal id, string idHashKey, int level)
        {
            StringBuilder str_level = new StringBuilder();
            str_level.Append(idHashKey);

            if (level > 0)
            {
                //hash: 00000_____
                for (int i = 0; i < level; i++)
                {
                    str_level.Append("_____");
                }
            }
            else if (level < 0)
            {
                //если указан -1 то искать и свои
                if (string.IsNullOrEmpty(idHashKey)) str_level.Append("%");
                else str_level.Append("%");
            }
            else
            {
                //hash: 00000%
                if (string.IsNullOrEmpty(idHashKey)) str_level.Append("%");
                else str_level.Append("_%");
            }
            return str_level.ToString();
        }

        public static string GetHashCode(decimal id_node, int level, Хранилище хранилище, string domain)
        {
            //using(new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = string.Format("select [HashCode] from {0}tblNode WITH(NOLOCK) where id_node = @id_node", DataClient.GetTablePrefix(хранилище));
                    command.Parameters.AddWithValue("@id_node", id_node).SqlDbType = SqlDbType.Decimal;
                    return GetHashKeyLevel(id_node, Convert.ToString(command.ExecuteScalar()), level);
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        #endregion

        #region DataBinding
        private static object cacheObjectBinding = new System.Object();
        public Binding[] Binder_СписокСвязей(string Name, string domain)
        {
            var _hashquery = Cache.Key<КешФорма>(domain, Name);
            var cacheItem = Cache.Get<КешФорма>(_hashquery);
            if (cacheItem != null)
            {
                return cacheItem.Items;
            }
            else
            {
                var data = new DataClient();
                var query = new Query();
                query.Типы.Add("DataBindItem");
                query.ДобавитьВыводимыеКолонки(new string[] { "attribute", "control", "StringFormat", "PropertyPath" /*, "Valids"*/ });
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "id_type", Значение = GetIdTypeByName(Name, domain), Оператор = Query.Оператор.Равно });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.ПривязкиКФормам), МаксимальнаяГлубина = 1 });
                var items = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().Select(p => new Binding()
                {
                    attribute = p.Field<string>("attribute"),
                    control = p.Field<string>("control"),
                    PropertyPath = p.Field<string>("PropertyPath"),
                    StringFormat = p.Field<string>("StringFormat"),
                });

                cacheItem = new КешФорма() { Items = items.ToArray() };
                Cache.Set(_hashquery, cacheItem);
                return cacheItem.Items;
            }
        }
        public void Binder_СохранитьСвязь(string Name, string attribute, string control, string PropertyPath, string StringFormat, string domain)
        {
            if (string.IsNullOrEmpty(attribute)) return;
            var id_type = GetIdTypeByName(Name, domain);

            var data = new DataClient();
            var query = new Query();
            query.Типы.Add("DataBindItem");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "id_type",
                Значение = id_type,
                Оператор = Query.Оператор.Равно
            });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "control",
                Значение = control,
                Оператор = Query.Оператор.Равно
            });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "PropertyPath",
                Значение = PropertyPath,
                Оператор = Query.Оператор.Равно
            });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.ПривязкиКФормам), МаксимальнаяГлубина = 1 });
            var items = data.Поиск(query, Хранилище.Конфигурация, domain).Значение;

            var id_bind = 0M;
            if (items.Rows.Count > 0)
            {
                id_bind = Convert.ToDecimal(items.Rows[0]["id_node"]);
            }
            else
            {
                id_bind = data.ДобавитьРаздел(Convert.ToDecimal(СистемныеПапки.ПривязкиКФормам), "DataBindItem", null, false, Хранилище.Конфигурация, null, domain);
            }

            var values = new Dictionary<string, Value>();
            values.Add("НазваниеОбъекта", new Value(string.Format("{0:f0}_{1}_{2}", Name, attribute, control)));
            values.Add("id_type", new Value(id_type));
            values.Add("attribute", new Value(attribute));
            values.Add("control", new Value(control));
            values.Add("PropertyPath", new Value(PropertyPath));
            values.Add("StringFormat", new Value(StringFormat));
            //values.Add("Valids", new Value(Valids));            
            data.СохранитьЗначение(id_bind, values, false, Хранилище.Конфигурация, "WebService", domain);
        }
        public void Binder_УдалитьСвязи(string Name, string domain)
        {
            var id_type = GetIdTypeByName(Name, domain);
            var query = new Query();
            query.Типы.Add("DataBindItem");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "id_type", Значение = id_type, Оператор = Query.Оператор.Равно });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.ПривязкиКФормам), МаксимальнаяГлубина = 1 });
            new DataClient().УдалитьРазделПоиск(false, false, query, Хранилище.Конфигурация, "WebService", domain);

            Cache.RemoveAll(Cache.Key<КешФорма>(domain, Name));
        }
        #endregion

        #region Event
        public Event[] Event_СписокСобытий(string Name, string domain)
        {
            var data = new DataClient();
            var query = new Query();
            query.Типы.Add("Event");
            query.ДобавитьВыводимыеКолонки(new string[] { "control", "ИмяСобытия", "ИмяФункции" });
            //query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = Name, Оператор = Query.Оператор.Равно });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "id_type", Значение = GetIdTypeByName(Name, domain), Оператор = Query.Оператор.Равно });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.СобытияКФормам), МаксимальнаяГлубина = 1 });
            var items = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().
                Select(p => new Event()
                {
                    control = Convert.ToString(p.Field<object>("control")),
                    ИмяСобытия = Convert.ToString(p.Field<object>("ИмяСобытия")),
                    ИмяФункции = Convert.ToString(p.Field<object>("ИмяФункции"))
                });
            return items.ToArray();
        }
        public void Event_СохранитьСобытие(string Name, string control, string ИмяСобытия, string ИмяФункции, string domain)
        {
            if (string.IsNullOrEmpty(control) ||
                string.IsNullOrEmpty(ИмяСобытия) ||
                string.IsNullOrEmpty(ИмяФункции)) return;

            var id_type = GetIdTypeByName(Name, domain);
            var data = new DataClient();
            var query = new Query();
            query.Типы.Add("Event");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "id_type",
                Значение = id_type,
                Оператор = Query.Оператор.Равно
            });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "control",
                Значение = control,
                Оператор = Query.Оператор.Равно
            });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "ИмяСобытия",
                Значение = ИмяСобытия,
                Оператор = Query.Оператор.Равно
            });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.СобытияКФормам), МаксимальнаяГлубина = 1 });
            var items = data.Поиск(query, Хранилище.Конфигурация, domain).Значение;

            decimal id_bind = 0;
            if (items.Rows.Count > 0)
            {
                id_bind = Convert.ToDecimal(items.Rows[0]["id_node"]);
            }
            else
            {
                id_bind = data.ДобавитьРаздел(Convert.ToDecimal(СистемныеПапки.СобытияКФормам), "Event", null, false, Хранилище.Конфигурация, null, domain);
            }

            var values = new Dictionary<string, Value>();
            values.Add("НазваниеОбъекта", new Value(string.Format("{0:f0}_{1}_{2}", Name, control, ИмяСобытия)));
            values.Add("id_type", new Value(id_type));
            values.Add("control", new Value(control));
            values.Add("ИмяСобытия", new Value(ИмяСобытия));
            values.Add("ИмяФункции", new Value(ИмяФункции));
            data.СохранитьЗначение(id_bind, values, false, Хранилище.Конфигурация, "", domain);
        }
        public void Event_УдалитьСобытие(string Name, string domain)
        {
            var id_type = GetIdTypeByName(Name, domain);
            var query = new Query();
            query.Типы.Add("Event");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска()
            {
                Атрибут = "id_type",
                Значение = id_type,
                Оператор = Query.Оператор.Равно
            });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.СобытияКФормам), МаксимальнаяГлубина = 1 });
            new DataClient().УдалитьРазделПоиск(false, false, query, Хранилище.Конфигурация, "WebService", domain);
        }
        #endregion

        #region Процессы
        public decimal Процесс_СоздатьПроцесс(string НазваниеПроцесса, string Описание, string Тип, string user, string domain)
        {
            var data = new DataClient();
            //var ВремяНачалаПроцесса = DateTime.Now;
            var values = new Dictionary<string, Value>(4);
            values.Add("НазваниеОбъекта", new Value(НазваниеПроцесса));
            values.Add("Описание", new Value(Описание));
            //values.Add("ВремяНачалаПроцесса", new Value(ВремяНачалаПроцесса));
            values.Add("СтатусПроцесса", new Value("В работе"));
            values.Add("ТекущееСостояниеПроцесса", new Value(0));

            return data.ДобавитьРаздел(
                "Процессы",
                string.IsNullOrEmpty(Тип) ? "Процесс" : Тип,
                values,
                false,
                Хранилище.Оперативное,
                user, domain);
        }
        
        public void Процесс_ОбновитьСостояниеПроцесса(decimal Процесс, double СостояниеПроцесса, string domain)
        {
            if (Процесс == 0) return;

            try
            {
                var values = new Dictionary<string, Value>(2);
                values.Add("ТекущееСостояниеПроцесса", new Value(СостояниеПроцесса));
                values.Add("ВремяРаботыПроцесса", new Value(Convert.ToString(DateTime.Now - new DataClient().ПолучитьЗначение<DateTime>(Процесс, "ДатаСозданияОбъекта", Хранилище.Оперативное, domain))));
                new DataClient().СохранитьЗначение(Процесс, values, false, Хранилище.Оперативное, "", domain);
            }
            catch (Exception ex)
            {
                WindowsLog(ex.ToString(), "", domain);
            }
        }
        public void Процесс_ЗавершитьПроцесс(decimal Процесс, string domain)
        {
            if (Процесс == 0) return;

            Task.Factory.StartNew(delegate()
            {
                try
                {
                    var values = new Dictionary<string, Value>(4);
                    values.Add("ВремяЗавершенияПроцесса", new Value(DateTime.Now));
                    values.Add("СтатусПроцесса", new Value("Завершен"));
                    values.Add("ТекущееСостояниеПроцесса", new Value(100));
                    values.Add("ВремяРаботыПроцесса", new Value(Convert.ToString(DateTime.Now - new DataClient().ПолучитьЗначение<DateTime>(Процесс, "ДатаСозданияОбъекта", Хранилище.Оперативное, domain))));
                    new DataClient().СохранитьЗначение(Процесс, values, false, Хранилище.Оперативное, "", domain);
                }
                catch (Exception ex)
                {
                    WindowsLog(ex.ToString(), "", domain);
                }
            });
        }
        public void Процесс_ОшибкаВПроцессе(decimal Процесс, string СообщениеОбОшибке, string domain)
        {
            if (Процесс == 0) return;

            Task.Factory.StartNew(delegate()
            {
                try
                {
                    var values = new Dictionary<string, Value>(3);
                    values.Add("ВремяЗавершенияПроцесса", new Value(DateTime.Now));
                    values.Add("СтатусПроцесса", new Value("Ошибка"));
                    values.Add("СообщениеОбОшибке", new Value(СообщениеОбОшибке));
                    new DataClient().СохранитьЗначение(Процесс, values, false, Хранилище.Оперативное, "", domain);
                }
                catch (Exception ex)
                {
                    WindowsLog(ex.ToString(), "", domain);
                }
            });
        }
        #endregion

        #region Пользователи
        internal static IEnumerable<string> Domains;
        private static object lockDomains = new System.Object();
        public string[] СписокДоменов()
        {
            if (Domains == null)
            {
                lock (lockDomains)
                {
                    try
                    {
                        var domains = new List<string>();
                        foreach (var connectionStr in ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>()
                            .Where(p => p.ProviderName == "System.Data.SqlClient")
                            .GroupBy(p => new SqlConnectionStringBuilder(p.ConnectionString).DataSource)
                            .Select(p => p.First()))
                        {
                            var connection = new SqlConnection(string.Format(connectionStr.ConnectionString, "master"));
                            //using (new TransactionScope(TransactionScopeOption.Suppress))
                            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(connection))
                            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                            {
                                var command = (db.Connection as SqlConnection).CreateCommand();
                                command.CommandText = "SELECT [name] FROM master.dbo.sysdatabases WITH(NOLOCK) WHERE [name] NOT IN ('master','model','msdb','tempdb') and [version] is not null";
                                command.CommandTimeout = 15;
                                try
                                {
                                    new SqlDataAdapter(command).Fill(table);

                                    if (command.Connection.State != ConnectionState.Open)
                                        command.Connection.Open();

                                    foreach (var item in table.Tables[0].AsEnumerable())
                                    {
                                        try
                                        {
                                            command = (db.Connection as SqlConnection).CreateCommand();
                                            command.CommandText = string.Format("select ISNULL(COUNT(*),0) from [{0}].INFORMATION_SCHEMA.TABLES WITH(NOLOCK) where TABLE_NAME = 'tblValueString'", item["name"]);
                                            if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                                            {
                                                domains.Add(item.Field<string>("name"));
                                            }
                                        }
                                        catch (SqlException)
                                        {
                                        }
                                    }
                                }
                                catch
                                {
                                }
                                finally
                                {
                                    command.Connection.Close();
                                }
                            }
                        }
                        Domains = domains.Distinct().OrderBy(p => p);
                        return Domains.ToArray();
                    }
                    catch
                    {
                        return new string[0];
                    }
                }
            }
            else
            {
                return Domains.ToArray();
            }
        }
        #endregion

        #region Авторизация
        private static bool ВыключитьБлокировки;
        private class AuthorizationBlock
        {
            public int Count { get; set; }
            public DateTime? Date { get; set; }
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AuthorizationBlock> blocks = new System.Collections.Concurrent.ConcurrentDictionary<string, AuthorizationBlock>();
        public Пользователь Авторизация(string UserName, string Password, bool ЗаписатьВЖурнал, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return null;

            //проверка лицензии
            //var result = RosService.Security.Security.ValidDbAsync(domain);
            //if (!string.IsNullOrEmpty(result))
            //    throw new Exception(result);

            var __hash = domain + "@" + UserName;
            var userBlock = null as AuthorizationBlock;
            if (!ВыключитьБлокировки && blocks.TryGetValue(__hash, out userBlock) && userBlock.Date != null)
            {
                if (DateTime.Now < userBlock.Date.Value)
                    throw new Exception(string.Format("Учетная заблокированна на 10 минут до {0}, из-за попытки подбора пароля.", userBlock.Date));
                else
                    blocks.TryRemove(__hash, out userBlock);
            }

            try
            {
                #region get host
                var IpAdress = ПолучитьIpАдресСоединения();
                var host = IpAdress;
                try
                {
                    host = System.Net.Dns.GetHostByAddress(IpAdress).HostName;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    host = IpAdress;
                }
                #endregion

                #region Найти пользователя
                var data = new DataClient();
                var user = QueryBuilder.ResolveIdNode("USER:" + UserName.ToUpper(), Хранилище.Оперативное, domain, false);
                if (user == 0)
                {
                    var __user = new RosService.Services.ServicesClient().НайтиПользователяПоЛогину(UserName, domain);
                    if (__user != null)
                        user = __user.id_node;
                }
                if (user == 0)
                    throw new Exception("Не верно указан логин или пароль пользователя.");
                #endregion

                var values = data.ПолучитьЗначение(user,
                    new string[] { "СсылкаНаГруппуПользователей", "ПарольПользователя", 
                        "РазрешитьВход", "Права.ЗапретитьУдаленныеПодключения", "Тип.Имя",
                        "ЛогинПользователя", "IpAdress", "Безопасность.ПривязатьIp" },
                    Хранилище.Оперативное, domain);

                if (Convert.ToString(values["ПарольПользователя"].Значение) != Password)
                {
                    var log = new StringBuilder(new DataClient().ПолучитьЗначение<string>(user, "Безопасность.ЖурналПодключений", Хранилище.Оперативное, domain));
                    log.Insert(0, string.Format("{0},{1},{2},,Не верный пароль: {3}\n", DateTime.Now, IpAdress, host, Password));

                    var v = new Dictionary<string, Value>();
                    v.Add("Безопасность.ЖурналПодключений", new Value(log.ToString()));
                    new DataClient().СохранитьЗначение(user, v, false, Хранилище.Оперативное, UserName, domain);

                    throw new Exception("Не верно указан логин или пароль пользователя.");
                }
                else if (!string.IsNullOrEmpty(Convert.ToString(values["РазрешитьВход"].Значение)) && !Convert.ToBoolean(values["РазрешитьВход"].Значение))
                {
                    throw new Exception("Доступ к системе приостановлен.");
                }

                var СсылкаНаГруппуПользователей = Convert.ToDecimal(values["СсылкаНаГруппуПользователей"].Значение);
                if (СсылкаНаГруппуПользователей == 0)
                    throw new Exception("Не задана группа пользователя, свяжитесь с технической поддержкой (support@itrf.ru).");

                var group_values = data.ПолучитьЗначение(СсылкаНаГруппуПользователей, new string[] { "НазваниеОбъекта", "НаборПравДоступа" }, Хранилище.Оперативное, domain);
                var СсылкаНаГруппуПользователей_НазваниеОбъекта = Convert.ToString(group_values["НазваниеОбъекта"].Значение);
                var U = new Пользователь()
                {
                    Логин = Convert.ToString(values["ЛогинПользователя"].Значение),
                    id_node = user,
                    Тип = Convert.ToString(values["Тип.Имя"].Значение),
                    Роли = new string[] { Convert.ToString(values["Тип.Имя"].Значение), СсылкаНаГруппуПользователей_НазваниеОбъекта },
                    ГруппаРаздел = СсылкаНаГруппуПользователей,
                    Группа = СсылкаНаГруппуПользователей_НазваниеОбъекта,
                    Интерфейс = СсылкаНаГруппуПользователей_НазваниеОбъекта,
                    Права = (ПраваПользователя)Convert.ToInt32(group_values["НаборПравДоступа"].Значение),
                };

                #region Проверить блокировку по IP
                var IpCurrent = Convert.ToString(values["IpAdress"].Значение);
                if (true.Equals(values["Безопасность.ПривязатьIp"].Значение) && !string.IsNullOrEmpty(IpCurrent) && !IpAdress.Equals(IpCurrent))
                    throw new Exception("Доступ ограничен по IP");
                #endregion

                #region Запретить удаленные подключения
                if (true.Equals(values["Права.ЗапретитьУдаленныеПодключения"].Значение) && !"Техподдержка".Equals(U.Логин))
                {
                    var __ip = ipToUint(IpAdress);
                    if (!((ipToUint("0.0.0.0") <= __ip && __ip <= ipToUint("192.168.255.255")) || __ip == 1))
                        throw new Exception("Удаленные подключения (через Интернет) запрещены.");
                }
                #endregion

                #region Атрибуты
                //if ((U.Права & ПраваПользователя.ПоказатьОбъектыПоАтрибуту) == ПраваПользователя.ПоказатьОбъектыПоАтрибуту && string.IsNullOrEmpty(U.ПоисковыйАтрибут))
                //{
                //    throw new Exception("Не задан 'ПоисковыйАтрибут', свяжитесь с технической поддержкой (support@itrf.ru).");
                //}
                //if ((U.Права & ПраваПользователя.ПоказатьОбъектыПодструктуры) == ПраваПользователя.ПоказатьОбъектыПодструктуры && U.МестоПоиска <= 0)
                //{
                //    throw new Exception("Не задано 'МестоПоиска', свяжитесь с технической поддержкой (support@itrf.ru).");
                //}
                #endregion

                #region Записать в журнал
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        //сохранить время входа
                        var v = new Dictionary<string, Value>();
                        v.Add("ВремяСессии", new Value(DateTime.Now.AddMinutes(8)));
                        v.Add("ДатаАвторизации", new Value(DateTime.Now));
                        if(IpAdress != IpCurrent)
                            v.Add("IpAdress", new Value(IpAdress));
                        new DataClient().СохранитьЗначение(user, v, false, Хранилище.Оперативное, UserName, domain);

                        //var log = new StringBuilder(new DataClient().ПолучитьЗначение<string>(user, "Безопасность.ЖурналПодключений", Хранилище.Оперативное, domain));
                        //log.Insert(0, string.Format("{0},{1},{2},OK,\n", DateTime.Now, IpAdress, host));

                        //v = new Dictionary<string, Value>();
                        //v.Add("Безопасность.ЖурналПодключений", new Value(log.ToString()));
                        //new DataClient().СохранитьЗначение(user, v, false, Хранилище.Оперативное, UserName, domain);
                    }
                    catch (Exception)
                    {
                    }
                });
                #endregion

                if (userBlock != null)
                    blocks.TryRemove(__hash, out userBlock);

                return U;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 4060)
                {
                    throw new Exception("Не верно указан домен.");
                }
                else
                {
                    ConfigurationClient.WindowsLog(ex.Message, UserName, domain, EventLogEntryType.FailureAudit, ex.ToString());
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                #region блокировать учетную запись
                if (userBlock == null)
                {
                    userBlock = new AuthorizationBlock();
                    blocks.TryAdd(__hash, userBlock);
                }
                userBlock.Count = userBlock.Count + 1;
                if (userBlock.Count >= 10)
                {
                    userBlock.Date = DateTime.Now.AddMinutes(10);
                    userBlock.Count = 0;
                }
                #endregion

                ConfigurationClient.WindowsLog(ex.Message, UserName, domain, EventLogEntryType.FailureAudit, ex.ToString());
                throw ex;
            }
        }
        public void АвторизацияПродлитьСессиюПользователя(decimal СсылкаНаПользователя, TimeSpan ВремяСесси, string domain)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    if (СсылкаНаПользователя > 0)
                    {
                        new DataClient().СохранитьЗначениеПростое(СсылкаНаПользователя, "ВремяСессии", DateTime.Now.Add(ВремяСесси), false, Хранилище.Оперативное, "WebService", domain);

                        //#region В статистику
                        //try
                        //{
                        //    using (var w = new System.Net.WebClient())
                        //    {
                        //        var host = "g.itrf.ru";
                        //        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["СерверСтатистики"]))
                        //            host = ConfigurationManager.AppSettings["СерверСтатистики"];

                        //        w.DownloadStringAsync(new Uri(string.Format(@"http://{0}/session.ashx?key={1}::{2:f0}", host, domain, СсылкаНаПользователя)));
                        //    }
                        //}
                        //catch
                        //{
                        //}
                        //#endregion
                    }
                }
                catch
                {
                }
            });
        }
        public string ПолучитьIpАдресСоединения()
        {
            try
            {
                var context = OperationContext.Current;
                var properties = context.IncomingMessageProperties;
                var endpoint = properties[System.ServiceModel.Channels.RemoteEndpointMessageProperty.Name] as System.ServiceModel.Channels.RemoteEndpointMessageProperty;
                return endpoint.Address;
            }
            catch
            {
                return "Не удалось определить IP";
            }
        }
        public static uint ipToUint(string ip)
        {
            var ipBytes = System.Net.IPAddress.Parse(ip).GetAddressBytes();
            var bConvert = new System.ComponentModel.ByteConverter();
            uint ipUint = 0;

            int shift = 24; // indicates number of bits left for shifting
            foreach (byte b in ipBytes)
            {
                if (ipUint == 0)
                {
                    ipUint = (uint)bConvert.ConvertTo(b, typeof(uint)) << shift;
                    shift -= 8;
                    continue;
                }

                if (shift >= 8)
                    ipUint += (uint)bConvert.ConvertTo(b, typeof(uint)) << shift;
                else
                    ipUint += (uint)bConvert.ConvertTo(b, typeof(uint));

                shift -= 8;
            }

            return ipUint;
        }

        public void АвторизацияБлокировки(bool Выключить)
        {
            ВыключитьБлокировки = Выключить;
        }
        #endregion

        #region Основные
        public RosService.Configuration.Type ПолучитьТип(string Имя, string domain)
        {
            try
            {
                var _hashquery = Cache.Key<КешПолучитьТип>(domain, Имя);
                var cacheItem = Caching.Cache.Get<КешПолучитьТип>(_hashquery);
                if (cacheItem != null)
                {
                    return cacheItem.Тип;
                }
                else
                {
                    switch (Имя)
                    {
                        #region HashCode
                        case "HashCode":
                        case "@HashCode":

                            cacheItem = new КешПолучитьТип()
                            {
                                Тип = new RosService.Configuration.Type()
                                {
                                    BaseType = "string",
                                    //IsSystem = true,
                                    MemberType = MemberTypes.String,
                                    Name = Имя,
                                    Описание = Имя
                                }
                            };
                            Cache.Set(_hashquery, cacheItem);
                            return cacheItem.Тип;
                        #endregion

                        #region GuidCode
                        case "GuidCode":
                        case "@GuidCode":

                            cacheItem = new КешПолучитьТип()
                            {
                                Тип = new RosService.Configuration.Type()
                                {
                                    BaseType = "string",
                                    //IsSystem = true,
                                    MemberType = MemberTypes.Object,
                                    Name = Имя,
                                    Описание = Имя
                                }
                            };
                            Cache.Set(_hashquery, cacheItem);
                            return cacheItem.Тип;
                        #endregion

                        #region id_node
                        case "id_node":

                            cacheItem = new КешПолучитьТип()
                            {
                                Тип = new RosService.Configuration.Type()
                                {
                                    MemberType = MemberTypes.Undefined,
                                    Name = Имя,
                                    Описание = Имя
                                }
                            };
                            Cache.Set(_hashquery, cacheItem);
                            return cacheItem.Тип;
                        #endregion

                        #region @UpdateDate
                        case "@UpdateDate":
                            cacheItem = new КешПолучитьТип()
                            {
                                Тип = new RosService.Configuration.Type()
                                {
                                    BaseType = "datetime",
                                    IsReadOnly = true,
                                    MemberType = MemberTypes.DateTime,
                                    RegisterType = RegisterTypes.datetime_value,
                                    Name = Имя,
                                    Описание = Имя
                                }
                            };
                            Cache.Set(_hashquery, cacheItem);
                            return cacheItem.Тип;
                        #endregion

                        default:
                            
                            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                            {
                                var command = (db.Connection as SqlConnection).CreateCommand();
                                command.CommandText = @"
                                        set nocount on; 
                                        ------------------
                                        select * from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] = @Name";
                                command.Parameters.AddWithValue("@Name", Имя).SqlDbType = SqlDbType.VarChar;
                                new SqlDataAdapter(command).Fill(table);

                                if (table.Tables[0].Rows.Count > 0)
                                {
                                    cacheItem = new КешПолучитьТип() { Тип = new RosService.Configuration.Type(table.Tables[0].Rows[0]) };
                                    Cache.Set<КешПолучитьТип>(_hashquery, cacheItem);
                                    return cacheItem.Тип;
                                }
                                return null;
                            }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        public RosService.Configuration.Type[] СписокТипов(IEnumerable<string> СписокТиповДанных, string domain)
        {
            try
            {
                //using (new TransactionScope(TransactionScopeOption.Suppress))
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                {
                    if (СписокТиповДанных == null || СписокТиповДанных.Count() == 0)
                    {
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = @"
                        set nocount on
                        ---------------
                        select * from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and ([BaseType] = '' or [MemberType] = 1)";
                        new SqlDataAdapter(command).Fill(table);
                        return table.Tables[0].AsEnumerable().Select(p => new RosService.Configuration.Type(p)).ToArray();
                    }
                    else
                    {
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = string.Format(@"
                        set nocount on
                        ---------------
                        select * from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] in ({0})",
                        string.Join(",", СписокТиповДанных.Where(p => !string.IsNullOrEmpty(p)).Select(p => string.Format("'{0}'", Convert.ToString(p).Trim())).ToArray())
                        );
                        new SqlDataAdapter(command).Fill(table);
                        return table.Tables[0].AsEnumerable().Select(p => new RosService.Configuration.Type(p)).ToArray();
                    }
                }
            }
            catch
            {
                return new RosService.Configuration.Type[0];
            }
        }
        public RosService.Configuration.Type[] СписокАтрибутов(string ТипДанных, string domain)
        {
            var _hashquery = Cache.Key<КешСписокАтрибутов>(domain, ТипДанных);
            var cacheItem = Cache.Get<КешСписокАтрибутов>(_hashquery);
            if (cacheItem != null)
            {
                return cacheItem.Атрибуты;
            }
            else
            {
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                        //var s = System.Diagnostics.Stopwatch.StartNew();
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = @"
                        set nocount on
                        ---------------
                        ;with
                    	    attributes as (select * from assembly_tblAttribute WITH(NOLOCK) where id_parent = 
                                (select [id_type] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] = @ТипДанных)),
                    	    types as (select * from assembly_tblAttribute WITH(NOLOCK) where id_parent = 0)
                        select
                    	    attributes.* 
                        from
                    	    attributes WITH(NOLOCK)
                    	    left join types WITH(NOLOCK) on types.Name = attributes.ReflectedType
                        order by
                    	    types.TypeHashCode desc, attributes.Name";
                        command.Parameters.AddWithValue("@ТипДанных", ТипДанных).SqlDbType = SqlDbType.VarChar;
                        new SqlDataAdapter(command).Fill(table);

                        cacheItem = new КешСписокАтрибутов() 
                        { 
                            Атрибуты = table.Tables[0].AsEnumerable().Select(p => new RosService.Configuration.Type(p)).ToArray(),
                            //AvgTime = (uint)s.ElapsedMilliseconds,
                        };
                        Cache.Set(_hashquery, cacheItem);
                        return cacheItem.Атрибуты;
                    }
                    catch
                    {
                        return new RosService.Configuration.Type[0];
                    }
                    finally
                    {
                        db.Connection.Close();
                    }
                }
            }
        }

        [Obsolete("Не оптимизированная функция СписокНаследуемыхТипов", false)]
        public RosService.Configuration.Type[] СписокНаследуемыхТипов(string ТипДанных, bool ДобавитьБазовыйТип, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                var type = ПолучитьТип(ТипДанных, domain);
                if (type == null) return new RosService.Configuration.Type[0];

                if (ДобавитьБазовыйТип)
                {
                    return (from a in db.assembly_tblAttributes
                            where a.id_parent == 0 && a.TypeHashCode.StartsWith(type.TypeHashCode)
                            orderby a.Name
                            select new RosService.Configuration.Type(a)).ToArray();
                }
                else
                {
                    return (from a in db.assembly_tblAttributes
                            where a.id_parent == 0 && a.TypeHashCode.StartsWith(type.TypeHashCode) && a.TypeHashCode != type.TypeHashCode
                            orderby a.Name
                            select new RosService.Configuration.Type(a)).ToArray();
                }
            }
        }
        public RosService.Configuration.Type[] СписокЗависимыхТипов(string ТипДанных, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            {
                var type = ПолучитьТип(ТипДанных, domain);
                if (type == null) throw new Exception(string.Format("Тип данных '{0}' не найден.", ТипДанных));

                var command = (db.Connection as SqlConnection).CreateCommand();
                command.CommandText = @"
                    set nocount on
                    ---------------
                    ;with
	                    Атрибуты as (select * from dbo.assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0),
	                    СписокЗависимыхТипов as (select DISTINCT id_parent from dbo.assembly_tblAttribute WITH(NOLOCK) where [TypeHashCode] like @TypeHashCode)
                    select 
	                    Атрибуты.*
                    from
	                    СписокЗависимыхТипов WITH(NOLOCK)
	                    inner join Атрибуты WITH(NOLOCK) on Атрибуты.id_type = СписокЗависимыхТипов.id_parent
                    order by
	                    Атрибуты.TypeHashCode";
                command.Parameters.AddWithValue("@TypeHashCode", type.TypeHashCode + "%").SqlDbType = SqlDbType.VarChar;
                new SqlDataAdapter(command).Fill(table);
                return table.Tables[0].AsEnumerable().Select(p => new RosService.Configuration.Type(p)).ToArray();

                //                return db.ExecuteQuery<assembly_tblAttribute>(@"
                //                    set nocount on
                //                    ---------------
                //                    ;with
                //	                    Атрибуты as (select * from dbo.assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0),
                //	                    СписокЗависимыхТипов as (select DISTINCT id_parent from dbo.assembly_tblAttribute WITH(NOLOCK) where [TypeHashCode] like {0}+'%')
                //                    select 
                //	                    Атрибуты.*
                //                    from
                //	                    СписокЗависимыхТипов WITH(NOLOCK)
                //	                    inner join Атрибуты WITH(NOLOCK) on Атрибуты.id_type = СписокЗависимыхТипов.id_parent
                //                    order by
                //	                    Атрибуты.TypeHashCode", 
                //                    type.TypeHashCode).Select(p => new RosService.Configuration.Type(p)).ToArray();
            }
        }
        public string[] СписокКатегорий(string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            {
                var command = (db.Connection as SqlConnection).CreateCommand();
                command.CommandText = @"select distinct [Namespace] from assembly_tblAttribute WITH(NOLOCK)";
                new SqlDataAdapter(command).Fill(table);
                return table.Tables[0].AsEnumerable().Select(p => p.Field<string>("Namespace")).ToArray();

                //return db.assembly_tblAttributes.Select(p => p.Namespace).Distinct().ToArray();
            }
        }

        public string ДобавитьТип(decimal Номер, string Имя, string Описание, string Категория, string БазовыйТип, bool IsМассив, bool ОбновитьКонфигурацию, string user, string domain)
        {
            return ДобавитьТип(Номер, Имя, Описание, Категория, БазовыйТип, IsМассив, false, ОбновитьКонфигурацию, user, domain);
        }
        public string ДобавитьТип(decimal Номер, string Имя, string Описание, string Категория, string БазовыйТип, bool IsМассив, bool IsReadOnly, bool ОбновитьКонфигурацию, string user, string domain)
        {
            try
            {
                //using (TransactionScope scope = new TransactionScope())
                {
                    var data = new DataClient();
                    var values = new Dictionary<string, Value>();
                    if (string.IsNullOrEmpty(Имя)) Имя = ОписаниеВИмя(Описание);
                    using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                    {
                        //вычеслить уникальный номер
                        var hash = string.Join("", (new CRC16().ComputeHash(Имя)).Select(p => p.ToString()).ToArray());
                        var __d_type = Номер > 0 ? Номер : Convert.ToDecimal(hash);
                        try
                        {
                            if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
                            for (int i = 0; ; i++)
                            {
                                var command = (db.Connection as SqlConnection).CreateCommand();
                                command.CommandText = string.Format(@"select isnull(count(*),0) from assembly_tblAttribute WITH(NOLOCK) where id_type = @id_type and id_parent = 0");
                                command.Parameters.AddWithValue("@id_type", __d_type).SqlDbType = SqlDbType.Decimal;
                                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                                    break;
                                else if (i >= 100)
                                    throw new Exception(string.Format("Номер {0} типа данных '{1}' уже занят.", __d_type, Имя));

                                __d_type++;
                            }
                        }
                        finally
                        {
                            db.Connection.Close();
                        }


                        var РазделБазовыйТип = 0M;

                        //Проверить не добавлен ли уже такой тип данных
                        if (db.assembly_tblAttributes.SingleOrDefault(p => p.Name == Имя && p.id_parent == 0) != null)
                            throw new Exception(string.Format("Тип данных {0} уже создан.", Имя));
                        if (string.IsNullOrEmpty(Категория)) throw new Exception("Укажите категорию");
                        if (string.IsNullOrEmpty(Имя)) throw new Exception("Имя не может быть пустым");


                        //Найти раздел базовый тип
                        РазделБазовыйТип = НайтиРазделТипаДанных(БазовыйТип, domain);
                        if (РазделБазовыйТип == 0) throw new Exception("Не верно указан базовый тип.");

                        values = new Dictionary<string, Value>(10);
                        values.Add("НазваниеОбъекта", new Value(Описание));
                        values.Add("ИмяТипаДанных", new Value(Имя));
                        values.Add("КатегорияТипаДанных", new Value(Категория));
                        values.Add("БазовыйТип", new Value(РазделБазовыйТип));
                        values.Add("НомерТипаДаннах", new Value(__d_type));
                        values.Add("IsReadOnly", new Value(IsReadOnly));
                        

                        var id_node = data.ДобавитьРаздел(Convert.ToDecimal(СистемныеПапки.РазделТипы), "ТипДанных", values, false, Хранилище.Конфигурация, user, domain);
                        if (ОбновитьКонфигурацию)
                        {
                            RosService.Compile.Компилятор.КомпилироватьТипДанных(id_node, domain);

                            //После компилирования проставить всем типам описание
                            var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                            var type = ПолучитьТип(Имя, domain);
                            if (type.MemberType == MemberTypes.Object)
                            {
                                var v = new Dictionary<string, Value>();
                                v.Add("Тип.Описание", new Value(type.Описание));
                                //v.Add("Тип.Базовый", new Value(type.BaseType));
                                //v.Add("Тип.HashCode", new Value(type.TypeHashCode));
                                v.Add("Тип.Имя", new Value(type.Name));
                                v.Add("ИконкаПоУмолчанию", new Value(ПолучитьЗначение(БазовыйТип, "ИконкаПоУмолчанию", domain)));
                                v.Add("НазваниеОбъекта", new Value(ПолучитьЗначение(БазовыйТип, "НазваниеОбъекта", domain)));
                                
                                //перенести значения по умолчанию
                                var ds = new ConfigurationClient().СписокАтрибутов(БазовыйТип, domain);

                                //Сохранить все значения по-умолчанию
                                var def_value = ds.AsParallel()
                                    .Where(p => p.ReflectedType != "object")
                                    .Select(p => new { Name = p.Name, Value = ПолучитьЗначение(БазовыйТип, p.Name, domain) })
                                    .Where(p => p.Value != null && !("").Equals(p.Value));
                                foreach (var item in def_value)
                                {
                                    v.Add(item.Name, new Value(item.Value));
                                }
                                СохранитьЗначение(Имя, v, domain);
                            }
                        }
                    }
                    return Имя;
                }
            }
            catch (Exception ex)
            {
                ЖурналСобытийДобавитьОшибку(ex.Message, ex.ToString(), user, domain);
                throw ex;
            }
        }
        public void ДобавитьАтрибут(string ТипДанных, string Атрибут, bool ОбновитьКонфигурацию, string user, string domain)
        {
            try
            {
                //using (TransactionScope scope = new TransactionScope())
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    //Проверить не добавлен ли уже такой тип данных
                    var type = ПолучитьТип(ТипДанных, domain);
                    if (type == null) throw new Exception(string.Format("Тип данных '{0}' не найден.", ТипДанных));

                    //Проверить если такой тип данных
                    var attribute = ПолучитьТип(Атрибут, domain);
                    if (attribute == null) throw new Exception(string.Format("Тип данных '{0}' не найден.", Атрибут));

                    //Проверить не добавлени ли уже этот атрибут
                    if (db.assembly_tblAttributes.SingleOrDefault(p => p.Name == Атрибут && p.id_parent == GetIdTypeByName(ТипДанных, domain)) != null)
                        throw new Exception(string.Format("Атрибут '{0}' уже добавлен.", Атрибут));

                    //Найти раздел ТипаДанных
                    var РазделТипДанных = НайтиРазделТипаДанных(ТипДанных, domain);
                    if (РазделТипДанных == 0) throw new Exception(string.Format("Тип данных '{0}' не найден.", ТипДанных));

                    //Найти раздел Атрибут
                    var РазделАтрибут = НайтиРазделТипаДанных(Атрибут, domain);
                    if (РазделАтрибут == 0) throw new Exception(string.Format("Тип данных '{0}' не найден.", Атрибут));

                    var values = new Dictionary<string, Value>();
                    values.Add("СсылкаНаТипДанных", new Value(РазделАтрибут));
                    values.Add("НазваниеОбъекта", new Value(Атрибут));
                    new DataClient().ДобавитьРаздел(РазделТипДанных, "Атрибут", values, false, Хранилище.Конфигурация, user, domain);

                    if (ОбновитьКонфигурацию)
                    {
                        RosService.Compile.Компилятор.КомпилироватьЗависимыеТипыДанных(ТипДанных, domain);
                    }

                    Cache.RemoveAll(Cache.Key<КешСписокАтрибутов>(domain, ТипДанных));
                }
            }
            catch (Exception ex)
            {
                ЖурналСобытийДобавитьОшибку(ex.Message, ex.ToString(), user, domain);
                throw ex;
            }
        }

        public bool ПроверитьНаследование(string БазовыйТип, string Тип, string user, string domain)
        {
            if (БазовыйТип == Тип)
                return true;
            return СписокНаследуемыхТипов(БазовыйТип, false, domain).FirstOrDefault(p => p.Name == Тип) != null;
        }
        #endregion

        #region Helper
        internal decimal GetIdTypeByName(string Name, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = @"set nocount on; select [id_type] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] = @Name";
                    command.Parameters.AddWithValue("@Name", Name).SqlDbType = SqlDbType.VarChar;
                    return Convert.ToDecimal(command.ExecuteScalar());
                }
                catch
                {
                    return 0m;
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        internal string GetNameByIdType(decimal id_type, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = @"set nocount on; select [Name] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [id_type] = @id_type";
                    command.Parameters.AddWithValue("@id_type", id_type).SqlDbType = SqlDbType.Decimal;
                    return Convert.ToString(command.ExecuteScalar());
                }
                catch
                {
                    throw new Exception("Ошибка в функции 'GetNameByIdType'");
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        internal string GetNameByHashCode(string HashCode, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = @"set nocount on; select [Name] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [HashCode] = @HashCode";
                    command.Parameters.AddWithValue("@HashCode", HashCode).SqlDbType = SqlDbType.VarChar;
                    return Convert.ToString(command.ExecuteScalar());
                }
                catch
                {
                    throw new Exception("Ошибка в функции 'GetNameByIdType'");
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        #endregion

        #region Удаление
        public void УдалитьТип(string ТипДанных, string domain)
        {
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            {
                var data = new DataClient();
                var СписокЗависимыхТипов = this.СписокЗависимыхТипов(ТипДанных, domain);
                if (СписокЗависимыхТипов.Count() > 1) 
                    throw new Exception(string.Format("Не возможно удалить, найдено связанных объектов {0}. [{1}]",
                        СписокЗависимыхТипов.Count(), string.Join(", ", СписокЗависимыхТипов.Select(p => p.Name))));

                Binder_УдалитьСвязи(ТипДанных, domain);
                Event_УдалитьСобытие(ТипДанных, domain);

                db.ExecuteCommand("delete from assembly_tblAttribute where id_parent = {0} or id_type = {0}", new ConfigurationClient().GetIdTypeByName(ТипДанных, domain));

                //удалить тип данных
                var query = new Query();
                query.Типы.Add("ТипДанных");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = ТипДанных, ИгрнорироватьПараметр = true });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.РазделТипы), МаксимальнаяГлубина = 1 });
                var id_node = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().Single().Field<decimal>("id_node");
                data.УдалитьРазделПоиск(false, false, query, Хранилище.Конфигурация, "WebService", domain);

                //удалить атрибуты
                query = new Query();
                query.Типы.Add("Атрибут");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаТипДанных", Значение = id_node });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = 4712, МаксимальнаяГлубина = 1 });
                data.УдалитьРазделПоиск(false, false, query, Хранилище.Конфигурация, "WebService", domain);

                Cache.Remove(Cache.Key<КешСписокАтрибутов>(domain, ТипДанных));
                Cache.Remove(Cache.Key<КешПолучитьТип>(domain, ТипДанных));
                Cache.RemoveAll(MemoryCache.Path(domain, Хранилище.Конфигурация, "default") + ТипДанных + ":");
            }
        }
        public void УдалитьАтрибут(string ТипДанных, string Атрибут, string domain)
        {
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            {
                var data = new DataClient();
                var type = ПолучитьТип(ТипДанных, domain);
                if (type == null) throw new Exception(string.Format("Тип данных '{0}' не найден.", ТипДанных));
                var attribute = ПолучитьТип(Атрибут, domain);
                if (attribute == null) throw new Exception(string.Format("Тип данных '{0}' не найден.", Атрибут));

                //Найти раздел ТипаДанных
                var query = new Query();
                query.Типы.Add("ТипДанных");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = ТипДанных, ИгрнорироватьПараметр = true });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.РазделТипы), МаксимальнаяГлубина = 1 });
                var РазделТипДанных = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().SingleOrDefault().Field<decimal>("id_node");

                //Удалить атрибут
                query = new Query();
                query.Типы.Add("ТипДанных");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = Атрибут, ИгрнорироватьПараметр = true });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.РазделТипы), МаксимальнаяГлубина = 1 });
                var РазделАтрибут = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().SingleOrDefault().Field<decimal>("id_node");

                query = new Query();
                query.Типы.Add("Атрибут");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаТипДанных", Значение = РазделАтрибут });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = РазделТипДанных, МаксимальнаяГлубина = 1 });
                data.УдалитьРазделПоиск(false, false, query, Хранилище.Конфигурация, "WebService", domain);

                Cache.Remove(Cache.Key<КешСписокАтрибутов>(domain, ТипДанных));
            }

            System.Threading.Tasks.Task.Factory.StartNew(delegate()
            {
                RosService.Compile.Компилятор.КомпилироватьЗависимыеТипыДанных(ТипДанных, domain);
            });
        }
        #endregion

        #region Значения
        public object lockObjectSave = new Object();
        public T ПолучитьЗначение<T>(string Тип, string Атрибут, string domain)
        {
            try
            {
                var value = ПолучитьЗначение(Тип, Атрибут, domain);
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
            catch
            {
                return default(T);
            }
        }
        public object ПолучитьЗначение(string Тип, string Атрибут, string domain)
        {
            if (string.IsNullOrEmpty(Атрибут))
                return null;

            #region получить из кеша
            var __keyfull = MemoryCache.Path(domain, Хранилище.Конфигурация, "default") + Тип + ":" + Атрибут;
            //ConfigurationClient.WindowsLog(__keyfull, "777", domain, Тип, Атрибут);

            var contanier = MemoryCache.Get(__keyfull);
            if (contanier != null && contanier.obj != null)
            {
                return contanier.obj.Значение;
            }
            #endregion

            var attr = ПолучитьТип(Атрибут, domain);
            if (attr == null) return null;

            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) 
                        db.Connection.Open();
                    var command = (db.Connection as SqlConnection).CreateCommand();

                    //для совместимости
                    var id_type = 0m;
                    if (decimal.TryParse(Тип, out id_type) && id_type > 0)
                        Тип = GetNameByIdType(id_type, domain);

                    #region sql
                    command.CommandText = string.Format(@"
                        set nocount on
                        ---------------
                        declare @id_node numeric(18,0)
                        select @id_node = [id_node] from assembly_tblNode WITH(NOLOCK) where [id_parent] = @РазделЗначения and [type] = @type
                            
                        if(@RegisterType = 'double_value' and @MemberType = 6)
                        begin
                            declare @bool_value bit
                            select @bool_value = convert(bit, isnull([double_value],0)) from assembly_tblValueBool WITH(NOLOCK) where [id_node] = @id_node and [type] = @attribute;
                            if(@bool_value is null) set @bool_value = 0
                            select @bool_value
                        end
                        else if(@RegisterType = 'double_value')
                            select [double_value] from assembly_tblValueDouble WITH(NOLOCK) where [id_node] = @id_node and [type] = @attribute;
                        else if(@RegisterType = 'datetime_value')
                            select [datetime_value] from assembly_tblValueDate WITH(NOLOCK) where [id_node] = @id_node and [type] = @attribute;
                        else if(@RegisterType = 'byte_value')
                            select [byte_value] from assembly_tblValueByte WITH(NOLOCK) where [id_node] = @id_node and [type] = @attribute;
                        else 
                            select [string_value] from assembly_tblValueString WITH(NOLOCK) where [id_node] = @id_node and [type] = @attribute");
                    #endregion

                    command.Parameters.AddWithValue("@РазделЗначения", (int)СистемныеПапки.РазделЗначения).SqlDbType = SqlDbType.Int;
                    command.Parameters.AddWithValue("@attribute", Атрибут).SqlDbType = SqlDbType.VarChar;
                    command.Parameters.AddWithValue("@type", Тип).SqlDbType = SqlDbType.VarChar;

                    command.Parameters.AddWithValue("@RegisterType", attr.RegisterType.ToString()).SqlDbType = SqlDbType.VarChar;
                    command.Parameters.AddWithValue("@MemberType", (int)attr.MemberType).SqlDbType = SqlDbType.Int;

                    var value = command.ExecuteScalar();
                    if (Convert.IsDBNull(value)) 
                        value = null;

                    MemoryCache.Set(__keyfull, new ValueContanier()
                    {
                        obj = new Value(value),
                        cache = DateTime.Now.Add(MemoryCache.Timeout)
                    });
                    return value;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        public void СохранитьЗначение(string Тип, string attribute, object value, string domain)
        {
            var values = new Dictionary<string, Value>();
            values.Add(attribute, new Value(value));

            //if ("@Xaml".Equals(attribute) || "@ИсходныйКод".Equals(attribute))
            //{
            //    values.Add("@UpdateDate", new Value(DateTime.Now));
            //}

            СохранитьЗначение(Тип, values, domain);
        }
        public void СохранитьЗначение(string Тип, Dictionary<string, Value> values, string domain)
        {
            if (values == null || values.Count == 0)
                return;

            try
            {
                var id_node = null as object;
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
                        SqlCommand command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = "select [id_node] from assembly_tblNode WITH(NOLOCK) where [id_parent] = @РазделЗначения and [type] = @type";
                        command.Parameters.AddWithValue("@РазделЗначения", (int)СистемныеПапки.РазделЗначения).SqlDbType = SqlDbType.Int;
                        command.Parameters.AddWithValue("@type", Тип).SqlDbType = SqlDbType.VarChar;
                        id_node = command.ExecuteScalar();
                    }
                    finally
                    {
                        db.Connection.Close();
                    }
                }

                lock (lockObjectSave)
                {
                    if (id_node == null || Convert.IsDBNull(id_node) || Convert.ToDecimal(id_node) == 0)
                    {
                        id_node = new DataClient().ДобавитьРаздел(Convert.ToDecimal(СистемныеПапки.РазделЗначения), Тип, null, false, Хранилище.Конфигурация, null, domain);
                    }
                }

                var __path = MemoryCache.Path(domain, Хранилище.Конфигурация, "default") + Тип + ":";
                foreach (var item in values)
                {
                    MemoryCache.Set(__path + item.Key, new ValueContanier()
                    {
                        obj = item.Value,
                        cache = DateTime.Now.Add(MemoryCache.Timeout)
                    });
                }
                new DataClient().СохранитьЗначение(Convert.ToDecimal(id_node), values, false, Хранилище.Конфигурация, string.Empty, domain);
            }
            catch (Exception ex)
            {
                WindowsLog(ex.ToString(), "", domain, "Конфигуратор.СохранитьЗначение");
            }
            finally
            {
                //сбросить кеш на добавление разделов
                Cache.RemoveAll(Cache.Key<КешДобавитьРаздел>(domain, Тип));
                Cache.RemoveAll(Cache.Key<КешПолучитьТип>(domain, Тип));
                Cache.RemoveAll(Cache.Key<КешСписокАтрибутов>(domain, Тип));
                Cache.RemoveAll(Cache.Key<КешСчётчик>(domain, Тип));
            }
        }
        #endregion

        #region Сервисные
        public Форма ПолучитьФорму(string ТипДанных, string domain)
        {
            var _Форма = new Форма();
            Parallel.Invoke(
                delegate() { _Форма.Xaml = Convert.ToString(ПолучитьЗначение(ТипДанных, "@Xaml", domain)); },
                delegate() { _Форма.ИсходныйКод = Convert.ToString(ПолучитьЗначение(ТипДанных, "@ИсходныйКод", domain)); },
                delegate() { _Форма.Bindings = Binder_СписокСвязей(ТипДанных, domain); },
                delegate() { _Форма.Events = Event_СписокСобытий(ТипДанных, domain); }
            );
            return _Форма;

            //return new Форма()
            //{
            //    Xaml = Convert.ToString(ПолучитьЗначение(ТипДанных, "@Xaml", domain)),
            //    ИсходныйКод = Convert.ToString(ПолучитьЗначение(ТипДанных, "@ИсходныйКод", domain)),
            //    Bindings = Binder_СписокСвязей(ТипДанных, domain),
            //    Events = Event_СписокСобытий(ТипДанных, domain)
            //};
        }

        public decimal Сервис_ДобавитьВебСервис(string Адрес, string Название, string user, string domain)
        {
            var path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "Tools"); //, System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath);
            var file_cs = Path.Combine(path, Название + ".cs");
            var file_config = Path.Combine(path, Название + ".config");
            var file_assembly = Path.Combine(path, domain + "." + Название + ".dll");
            var Namespace = string.Format("RosService.Сервисы.{0}", Название);

            try
            {
                if (!Directory.Exists(path))
                    throw new Exception(string.Format("Не найдена утилита 'svcutil' в папке '{0}'", path));

                //System.Reflection.Assembly.GetAssembly(typeof(System.Data.DataTable)).Location
                var str = string.Format("{0} /l:c# /edb /importXmlTypes /r:{3} /out:{1} /config:{1} /namespace:*,{2}",
                            Адрес, Название, Namespace,
                            Path.Combine(Environment.GetEnvironmentVariable("windir"), @"Microsoft.NET\Framework\v2.0.50727\System.Data.dll"));
                var process = Process.Start(
                    new ProcessStartInfo()
                    {
                        WorkingDirectory = path,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "svcutil",
                        Arguments = str
                    });
                process.WaitForExit();
                //System.IO.File.WriteAllText(string.Format("{0}\\{1}.{2}.log", path, domain, Название) , process.StandardOutput.ReadToEnd());

                if (!System.IO.File.Exists(file_cs))
                    throw new Exception("Ошибка при чтении схемы веб-сервиса.\n" + path + "\n" + str);

                #region откомпилипровать веб-сервис и сохранить в виде сборки
                var source = System.IO.File.ReadAllText(file_cs);

                //add compiler parameters
                var compilerParams = new CompilerParameters();
                compilerParams.CompilerOptions = "/target:library"; // you can add /optimize
                compilerParams.GenerateExecutable = false;
                compilerParams.IncludeDebugInformation = false;
                compilerParams.OutputAssembly = file_assembly;

                // add some basic references
                compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
                compilerParams.ReferencedAssemblies.Add("System.dll");
                compilerParams.ReferencedAssemblies.Add("System.Core.dll");
                compilerParams.ReferencedAssemblies.Add("System.Data.dll");
                compilerParams.ReferencedAssemblies.Add("System.Data.DataSetExtensions.dll");
                compilerParams.ReferencedAssemblies.Add("System.Data.Linq.dll");
                compilerParams.ReferencedAssemblies.Add("System.Xml.dll");
                compilerParams.ReferencedAssemblies.Add("System.Xml.Linq.dll");
                compilerParams.ReferencedAssemblies.Add("System.Transactions.dll");
                compilerParams.ReferencedAssemblies.Add("WindowsBase.dll");
                compilerParams.ReferencedAssemblies.Add("UIAutomationProvider.dll");
                compilerParams.ReferencedAssemblies.Add("System.Runtime.Serialization.dll");
                compilerParams.ReferencedAssemblies.Add("System.ServiceModel.dll");

                using (CodeDomProvider codeProvider = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } }))
                {
                    //actually compile the code
                    var results = codeProvider.CompileAssemblyFromSource(compilerParams, source);

                    //Do we have any compiler errors
                    if (results.Errors.Count > 0)
                    {
                        var errors = new StringBuilder();
                        foreach (CompilerError error in results.Errors)
                            errors.AppendFormat("Line: {0}; Compile Error: {1}", error.Line, error.ErrorText);
                        throw new Exception(errors.ToString());
                    }
                }
                #endregion

                var assembly = System.IO.File.ReadAllBytes(compilerParams.OutputAssembly);
                var values = new Dictionary<string, Value>();
                values.Add("НазваниеОбъекта", new Value(Название));
                values.Add("ИдентификаторОбъекта", new Value(Название));
                values.Add("АдресВебСервиса", new Value(Адрес));
                values.Add("Namespace", new Value(Namespace));
                values.Add("AssemblyStream", new Value(assembly));
                values.Add("ConfigStream", new Value(System.IO.File.ReadAllBytes(file_config)));
                values.Add("РазмерФайла", new Value(assembly.Length));
                return new DataClient().ДобавитьРаздел("ВебСервисы", "WebСервис", values, false, Хранилище.Конфигурация, user, domain);
            }
            catch (Exception ex)
            {
                ЖурналСобытийДобавитьОшибку(ex.Message, ex.ToString(), user, domain);
            }
            finally
            {
                if (System.IO.File.Exists(file_cs)) System.IO.File.Delete(file_cs);
                if (System.IO.File.Exists(file_config)) System.IO.File.Delete(file_config);
                if (System.IO.File.Exists(file_assembly)) System.IO.File.Delete(file_assembly);
            }
            return 0;
        }
        public ВебСервис[] Сервис_СписокВебСервисов(string domain)
        {
            try
            {
                var query = new Query();
                query.Типы.Add("WebСервис");
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "НазваниеОбъекта" });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "АдресВебСервиса" });
                //query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "AssemblyStream" });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "Namespace" });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = "ВебСервисы" });
                return new DataClient().Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().Select(
                    p => new ВебСервис()
                    {
                        Название = p.Field<string>("НазваниеОбъекта"),
                        Адрес = p.Field<string>("АдресВебСервиса"),
                        Файл = new DataClient().ПолучитьЗначение<byte[]>(p.Field<decimal>("id_node"), "AssemblyStream", Хранилище.Конфигурация, domain),
                        Namespace = p.Field<string>("Namespace")
                    }).ToArray();
            }
            catch
            {
                return new ВебСервис[0];
            }
        }



        public string ОписаниеВИмя(string value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in System.Text.RegularExpressions.Regex.Replace(value, @"[^\w\d_.-@ ]", "").Split(' '))
            {
                if (string.IsNullOrEmpty(item)) continue;
                sb.AppendFormat("{0}{1}", item[0].ToString().ToUpper(), item.Length > 1 ? item.ToLower().Substring(1) : "");
            }
            return sb.ToString();
        }

        public decimal НайтиРазделТипаДанных(string Имя, string domain)
        {
            //Найти раздел базовый тип
            var query = new Query();
            query.Типы.Add("ТипДанных");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = Имя, ИгрнорироватьПараметр = true });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(СистемныеПапки.РазделТипы), МаксимальнаяГлубина = 1 });
            var node = new DataClient().Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().SingleOrDefault();
            if (node == null) return 0;
            return node.Field<decimal>("id_node");
        }
        public void КопироватьТипДанных(string Имя, string ИзДомена, string КопироватьВ, string КопироватьВДомен, УсловияКопирования УсловияКопирования, string user, string domain)
        {
            var trace = new StringBuilder();
            var domains = new List<string>();

            //исключим компирование во всех доменах для всех вложенных итераций
            var _УсловияКопирования = УсловияКопирования;
            if ((УсловияКопирования & УсловияКопирования.ВсеДомены) == УсловияКопирования.ВсеДомены)
            {
                domains.Add(КопироватьВДомен);
                _УсловияКопирования ^= УсловияКопирования.ВсеДомены;
            }
            else
            {
                //domains.Add(domain);
                domains.Add(КопироватьВДомен);                
            }

            var updateFrom = ПолучитьЗначение<DateTime?>(Имя, "@UpdateDate", ИзДомена);
            foreach (var _domain in domains)
            {
                if (updateFrom.HasValue)
                {
                    var updateTo = ПолучитьЗначение<DateTime?>(Имя, "@UpdateDate", КопироватьВДомен);
                    if (updateTo.HasValue && updateTo == updateFrom)
                        continue;
                }
                try
                {
                    var data = new DataClient();
                    if (string.IsNullOrEmpty(КопироватьВ)) КопироватьВ = Имя;
                    var ОригиналТипДанных = НайтиРазделТипаДанных(Имя, ИзДомена);
                    var КопияТипДанных = НайтиРазделТипаДанных(КопироватьВ, _domain);

                    if ((УсловияКопирования & УсловияКопирования.Атрибуты) == УсловияКопирования.Атрибуты)
                    {
                        if (КопияТипДанных == 0)
                        {
                            КопияТипДанных = data.ДобавитьРаздел(Convert.ToDecimal(СистемныеПапки.РазделТипы), "ТипДанных", null, false, Хранилище.Конфигурация, user, _domain);
                        }
                        else
                        {
                            data.УдалитьПодразделы(false, new decimal[] { КопияТипДанных }, Хранилище.Конфигурация, "WebService", _domain);
                        }

                        var values = new Dictionary<string, Value>();
                        foreach (var item in new ConfigurationClient().СписокАтрибутов("ТипДанных", ИзДомена))
                        {
                            if (item.IsReadOnly) continue;
                            if (item.IsAutoIncrement) continue;
                            if (item.Name == "ДатаСозданияОбъекта" || item.Name == "РедакторРаздела") continue;

                            values.Add(item.Name, new Value(data.ПолучитьЗначение<object>(ОригиналТипДанных, item.Name, Хранилище.Конфигурация, ИзДомена)));
                        }
                        data.СохранитьЗначение(КопияТипДанных, values, false, Хранилище.Конфигурация, user, _domain);

                        //Проверить занят ли оригинальный номер типа
                        var __d_type = data.ПолучитьЗначение<decimal>(ОригиналТипДанных, "НомерТипаДаннах", Хранилище.Конфигурация, ИзДомена);
                        if (ПолучитьТип(Имя, _domain) == null)
                        {
                            data.СохранитьЗначениеПростое(КопияТипДанных, "НомерТипаДаннах", __d_type, false, Хранилище.Конфигурация, user, _domain);
                        }

                        var БазовыйТип = data.ПолучитьЗначение<string>((decimal)values["БазовыйТип"].Значение, "ИмяТипаДанных", Хранилище.Конфигурация, ИзДомена);
                        var РазделБазовыйТип = НайтиРазделТипаДанных(БазовыйТип, _domain);
                        if (РазделБазовыйТип == 0)
                        {
                            КопироватьТипДанных(БазовыйТип, ИзДомена, null, КопироватьВДомен, _УсловияКопирования, user, _domain);
                            РазделБазовыйТип = НайтиРазделТипаДанных(БазовыйТип, _domain);
                        }
                        data.СохранитьЗначениеПростое(КопияТипДанных, "БазовыйТип", РазделБазовыйТип, false, Хранилище.Конфигурация, user, _domain);


                        //копировать атрибуты
                        var query = new Query();
                        query.Типы.Add("Атрибут");
                        query.ДобавитьВыводимыеКолонки(new string[] { "СсылкаНаТипДанных" });
                        query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = ОригиналТипДанных, МаксимальнаяГлубина = 1 });
                        foreach (var item in new DataClient().Поиск(query, Хранилище.Конфигурация, ИзДомена).AsEnumerable())
                        {
                            //проверить есть ли такой тип
                            var Атрибут = data.ПолучитьЗначение<string>(item.Field<decimal>("СсылкаНаТипДанных"), "ИмяТипаДанных", Хранилище.Конфигурация, ИзДомена);
                            var РазделАтрибут = НайтиРазделТипаДанных(Атрибут, _domain);
                            if (РазделАтрибут == 0)
                            {
                                КопироватьТипДанных(Атрибут, ИзДомена, null, КопироватьВДомен, _УсловияКопирования, user, _domain);
                                РазделАтрибут = НайтиРазделТипаДанных(Атрибут, _domain);
                            }

                            //Найти раздел Атрибут
                            values = new Dictionary<string, Value>();
                            values.Add("СсылкаНаТипДанных", new Value(РазделАтрибут));
                            values.Add("НазваниеОбъекта", new Value(Атрибут));
                            data.ДобавитьРаздел(КопияТипДанных, "Атрибут", values, false, Хранилище.Конфигурация, user, _domain);
                        }

                        RosService.Compile.Компилятор.КомпилироватьТипДанных(КопияТипДанных, _domain);
                        RosService.Compile.Компилятор.КомпилироватьЗависимыеТипыДанных(КопироватьВ, _domain);
                    }

                    if (КопияТипДанных == 0) 
                        throw new Exception(string.Format("Тип данных '{0}' в домене '{1}' не найден.", КопироватьВ, _domain));


                    if ((УсловияКопирования & УсловияКопирования.Шаблон) == УсловияКопирования.Шаблон)
                    {
                        var xaml = ПолучитьЗначение<string>(Имя, "@Xaml", ИзДомена);
                        if (string.IsNullOrEmpty(xaml) || xaml.StartsWith("{"))
                        {
                            СохранитьЗначение(Имя, "@Xaml", xaml, _domain);
                        }
                        else
                        {
                            //var id_type_original = data.ПолучитьЗначение<decimal>(ОригиналТипДанных, "НомерТипаДаннах", Хранилище.Конфигурация, ИзДомена);
                            //var id_type_copy = data.ПолучитьЗначение<decimal>(КопияТипДанных, "НомерТипаДаннах", Хранилище.Конфигурация, _domain);
                            if ((УсловияКопирования & УсловияКопирования.ИсходныйКод) == УсловияКопирования.ИсходныйКод)
                            {
                                СохранитьЗначение(Имя, "@ИсходныйКод", ПолучитьЗначение(Имя, "@ИсходныйКод", ИзДомена), _domain);

                                //копировать список событий
                                Event_УдалитьСобытие(Имя, _domain);
                                foreach (var item in Event_СписокСобытий(Имя, ИзДомена))
                                {
                                    Event_СохранитьСобытие(Имя, item.control, item.ИмяСобытия, item.ИмяФункции, _domain);
                                }
                            }
                            if ((УсловияКопирования & УсловияКопирования.Шаблон) == УсловияКопирования.Шаблон)
                            {
                                //СохранитьЗначение(Имя, "@Xaml", ПолучитьЗначение(Имя, "@Xaml", ИзДомена), _domain);
                                СохранитьЗначение(Имя, "@Xaml", xaml, _domain);

                                //копировать список привязок
                                Binder_УдалитьСвязи(Имя, _domain);
                                foreach (var item in Binder_СписокСвязей(Имя, ИзДомена))
                                {
                                    Binder_СохранитьСвязь(Имя, item.attribute, item.control, item.PropertyPath, item.StringFormat, _domain);
                                }
                            }
                        }

                    }
                    if ((УсловияКопирования & УсловияКопирования.ЗначенияПоУмолчанию) == УсловияКопирования.ЗначенияПоУмолчанию)
                    {
                        var attributes = СписокАтрибутов(Имя, ИзДомена);
                        foreach (var item in attributes)
                        {
                            if (item.Name == "ИконкаПоУмолчанию" || item.Name == "@Xaml" || item.Name == "@ИсходныйКод") continue;

                            var value = ПолучитьЗначение(Имя, item.Name, ИзДомена);
                            if (value != null && !value.Equals(string.Empty))
                            {
                                СохранитьЗначение(Имя, item.Name, value, _domain);
                            }
                        }
                    }
                    if ((УсловияКопирования & УсловияКопирования.Иконка) == УсловияКопирования.Иконка)
                    {
                        СохранитьЗначение(Имя, "ИконкаПоУмолчанию", ПолучитьЗначение(Имя, "ИконкаПоУмолчанию", ИзДомена), _domain);
                    }

                    if(updateFrom.HasValue)
                        СохранитьЗначение(Имя, "@UpdateDate", updateFrom, КопироватьВДомен);
                }
                catch (Exception ex)
                {
                    trace.AppendFormat("{0}: {1}\n", _domain, ex.ToString());
                }
            }

            if (!string.IsNullOrEmpty(trace.ToString()))
            {
                ConfigurationClient.WindowsLog("Configuration.КопироватьТипДанных", user, domain, trace.ToString());
                throw new Exception(trace.ToString());
            }
        }
        #endregion

        #region Конфигурация Создать / Очистить / Обновить
        public void КомпилироватьКонфигурацию(string domain)
        {
            RosService.Compile.Компилятор.КомпилироватьКонфигурацию(domain);

            //сбросить кеши
            Cache.RemoveAll(domain + ":D:");
            //Cache.RemoveAll(domain + ":C:");
        }
        #endregion

        #region Техническая поддержка
        public void ОтправитьИнструкцию(decimal id_user, string user, string domain)
        {
            var data = new DataClient();
            var login = data.ПолучитьЗначение<string>(id_user, "ЛогинПользователя", Хранилище.Оперативное, domain);
            var mail = data.ПолучитьЗначение<string>(id_user, "Email", Хранилище.Оперативное, domain);

            if (string.IsNullOrEmpty(login))
                throw new Exception(string.Format("Для отправки инструкции пользователю '{0}' необходимо указать логин и пароль для авторизации.", login));

            if (string.IsNullOrEmpty(mail))
                throw new Exception(string.Format("Для отправки инструкции пользователю '{0}' необходимо указать e-mail.", login));


            var sb = new StringBuilder();
            var group = data.ПолучитьЗначение<decimal>(id_user, "СсылкаНаГруппуПользователей", Хранилище.Оперативное, domain);
            sb.AppendFormat("Здравствуйте, {0}!\n", login);
            sb.Append("Воспользуйтесь инструкцией для правильной установки «Программа 4.0»\n\n");
            sb.Append("\thttp://росинфотех.рф/client/\n\n");
            sb.Append("Ваши персональные данные:\n\n");
            sb.AppendFormat("\tПользователь: {0}\\{1}\n", domain, login);
            sb.AppendFormat("\tПароль: {0}\n", data.ПолучитьЗначение<string>(id_user, "ПарольПользователя", Хранилище.Оперативное, domain));
            sb.AppendFormat("\tПрава доступа: {0}\n\n\n\n", data.ПолучитьЗначение<string>(group, "НазваниеОбъекта", Хранилище.Оперативное, domain));
            sb.Append("Для смены пароля, авторизуйтесь в программе и нажмите в верхнем меню пункт 'Файл' > 'Настройки пользователя'. В появившемся окне введите новый пароль и нажмите кнопку сохранить.\n\n");
            sb.Append("ВНИМАНИЕ: требуется наличие установленных обновлений Microsoft .NET Framework 4.0\n\n");
            sb.Append("http://www.microsoft.com/downloads/ru-ru/details.aspx?FamilyID=0A391ABD-25C1-4FC0-919F-B21F31AB88B7\n\n\n");
            sb.Append("----\n");
            sb.Append("Техническая поддержка support@itrf.ru\n");
            sb.Append("ООО «РосИнфоТех»\n\n");

            data.ОтправитьПисьмо(mail, string.Format("{0} :техническая поддержка", domain), sb.ToString(), null, false, user, domain);
        }
        public decimal ОтправитьПисьмоВТехническуюПоддержку(string ИмяДомена, string ОтКого, string ТемаСообщения, string ТекстСообщения, object СрокРеализации, bool Важно, Dictionary<string, byte[]> СписокФайлов, string user)
        {
            if (string.IsNullOrEmpty(ТемаСообщения))
            {
                ТемаСообщения = "В техническую поддержку";
            }
            if (СрокРеализации != null)
            {
                if (Convert.ToDateTime(СрокРеализации) <= DateTime.Today)
                    СрокРеализации = DateTime.Today;
                ТемаСообщения += string.Format(" до {0:d}", СрокРеализации);
            }
            if (Важно)
            {
                ТемаСообщения += " (важно)";
            }

            #region Сохранить в базу данных
            var domain = ConfigurationManager.AppSettings["ТехническаяПоддержка.БазаДанных"];
            var id_node = 0m;
            try
            {
                if (!string.IsNullOrEmpty(domain) && IsОтправитьПисьмоВТехническуюПоддержку(domain))
                {
                    var values = new Dictionary<string, Value>();
                    values.Add("ДатаНачала", new Value(DateTime.Now));
                    values.Add("Статус", new Value("В разработке"));
                    values.Add("НазваниеОбъекта", new Value(ТемаСообщения));
                    values.Add("Содержание", new Value(ТекстСообщения));
                    values.Add("ОтКого", new Value(ОтКого));
                    values.Add("ИмяДомена", new Value(ИмяДомена));
                    values.Add("Важно", new Value(Важно));
                    values.Add("СрокРеализации", new Value(СрокРеализации));
                    id_node = new DataClient().ДобавитьРаздел("ТехническиеЗадания", "ТехническоеЗадание", values, false, Хранилище.Оперативное, user, domain);

                    if (id_node > 0 && СписокФайлов != null)
                    {
                        foreach (var item in СписокФайлов)
                        {
                            new Files.FileClient().СохранитьФайлПолностью(id_node, "", item.Key, "", item.Value, Хранилище.Оперативное, user, domain);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WindowsLog(ex.Message, user, ИмяДомена);
            }
            #endregion

            #region Отправить оповещение на почту
            var mails = (ConfigurationManager.AppSettings["ТехническаяПоддержка.Emails"] ?? "").Split(';');
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["ТехническаяПоддержка.SMTP"]) && mails.Length > 0)
            {
                var default_mail = mails.First();
                var default_login = ConfigurationManager.AppSettings["ТехническаяПоддержка.Login"];
                var default_password = ConfigurationManager.AppSettings["ТехническаяПоддержка.Password"];
                var encoding = Encoding.UTF8;

                var smtpClient = new SmtpClient(ConfigurationManager.AppSettings["ТехническаяПоддержка.SMTP"]);
                smtpClient.SendCompleted += (object sender, System.ComponentModel.AsyncCompletedEventArgs e) =>
                {
                    var msg = e.UserState as MailMessage;

                    //при ошибке отправить напрямую в поддержку
                    if (e.Error != null && msg != null)
                    {
                        smtpClient = new SmtpClient(ConfigurationManager.AppSettings["ТехническаяПоддержка.SMTP"]);
                        smtpClient.Credentials = new System.Net.NetworkCredential(default_login, default_password);
                        msg.From = new MailAddress(default_mail);
                        smtpClient.SendAsync(msg, null);
                    }
                };


                var mail = ОтКого.Split(' ').FirstOrDefault(p => p.Contains("@"));
                if (!string.IsNullOrEmpty(mail))
                {
                    smtpClient.UseDefaultCredentials = true;
                }
                else
                {
                    mail = default_mail;
                    smtpClient.Credentials = new System.Net.NetworkCredential(default_mail, default_password);
                }

                #region Подготовить сообщение
                var sb = new StringBuilder();
                sb.AppendFormat("Домен: {0}\n", ИмяДомена);
                sb.AppendFormat("От кого: {0}\n\n", ОтКого);
                sb.AppendFormat("{0}", ТекстСообщения);

                #region добавить подпись
                try
                {
                    var Подпись = new DataClient().ПоискРазделаПоИдентификаторуОбъекта("Подпись:" + ИмяДомена, Хранилище.Оперативное, domain);
                    if (Подпись > 0)
                    {
                        var ТекстПодписи = new DataClient().ПолучитьЗначение<string>(Подпись, "ТекстПодписи", Хранилище.Оперативное, domain);
                        if (!string.IsNullOrEmpty(ТекстПодписи))
                        {
                            sb.AppendLine();
                            sb.AppendLine();
                            sb.AppendLine();
                            sb.Append("----------------");
                            sb.AppendLine();
                            sb.Append(ТекстПодписи);
                            sb.AppendLine();
                        }
                    }
                }
                catch(Exception)
                {
                }
                #endregion

                MailMessage oMsg = new MailMessage()
                {
                    IsBodyHtml = false,
                    From = new MailAddress(mail),
                    BodyEncoding = encoding,
                    SubjectEncoding = encoding,
                    Subject = string.Format("{0} :{1}", ИмяДомена, ТемаСообщения),
                    Body = sb.ToString(),
                    Priority = Важно ? MailPriority.High : MailPriority.Normal
                };
                if (СписокФайлов != null)
                {
                    foreach (var item in СписокФайлов)
                    {
                        MemoryStream ms = new MemoryStream(item.Value);
                        oMsg.Attachments.Add(new Attachment(ms, item.Key));
                    }
                }
                foreach (var item in mails)
                    oMsg.To.Add(item);
                #endregion
                smtpClient.SendAsync(oMsg, oMsg);
            }
            #endregion

            return id_node;
        }
        public bool IsОтправитьПисьмоВТехническуюПоддержку(string domain)
        {
            if (ПолучитьТип("ТехническоеЗадание", domain) != null)
                return true;

            if (QueryBuilder.ResolveIdNode("ТехническиеЗадания", Хранилище.Оперативное, domain, false) == 0)
            {
                var values = new Dictionary<string, Value>();
                values.Add("НазваниеОбъекта", new Value("Технические задания"));
                values.Add("ИдентификаторОбъекта", new Value("ТехническиеЗадания"));
                new DataClient().ДобавитьРаздел(1, "Папка", values, false, Хранилище.Оперативное, "", domain);

                try { ДобавитьТип(0, "ТехническоеЗадание", "Техническое задание", "System.Default", "object", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "ДатаНачала", "ДатаНачала", "System.Default", "datetime", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "Статус", "Статус", "System.Default", "string", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "Содержание", "Содержание", "System.Default", "string", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "ОтКого", "ОтКого", "System.Default", "string", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "ИмяДомена", "ИмяДомена", "System.Default", "string", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "Важно", "Важно", "System.Default", "bool", false, true, "", domain); }
                catch { }

                try { ДобавитьТип(0, "СрокРеализации", "СрокРеализации", "System.Default", "datetime", false, true, "", domain); }
                catch { }

                try { ДобавитьАтрибут("ТехническоеЗадание", "ДатаНачала", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "Статус", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "Содержание", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "ОтКого", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "ИмяДомена", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "Важно", true, "", domain); }
                catch { }
                try { ДобавитьАтрибут("ТехническоеЗадание", "СрокРеализации", true, "", domain); }
                catch { }
            }
            return true;
        }
        #endregion

        #region СписокЖурналов & СписокОтчетов & СписокСправочников
        public Журнал[] СписокЖурналов(string user, string domain)
        {
            return (from p in СписокНаследуемыхТипов("Журнал", false, domain)
                    orderby p.Описание
                    select new Журнал()
                    {
                        Имя = p.Name,
                        Описание = p.Описание,
                        Группа = Convert.ToString(ПолучитьЗначение(p.Name, "ГруппаЖурналов", domain))
                    }).ToArray();
        }
        public RosService.Configuration.Отчет[] СписокОтчетов(string user, string domain)
        {
            return СписокНаследуемыхТипов("Отчет", false, domain).Select(p => new RosService.Configuration.Отчет() { Имя = p.Name, Описание = p.Описание }).OrderBy(p => p.Описание).ToArray();
        }
        public Справочник[] СписокСправочников(string user, string domain)
        {
            var query = new Query();
            query.ДобавитьВыводимыеКолонки(new string[] { "НазваниеОбъекта", "ИдентификаторОбъекта" });
            query.Сортировки.Add(new Query.Сортировка() { Атрибут = "НазваниеОбъекта" });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = "Справочники", МаксимальнаяГлубина = 1 });
            return new DataClient().Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Select(p => new Справочник()
            {
                Имя = p.Field<string>("ИдентификаторОбъекта"),
                Описание = p.Field<string>("НазваниеОбъекта"),
                id_node = p.Field<decimal>("id_node")
            }).OrderBy(p => p.Описание).ToArray();
        }
        #endregion

        #region СписокКешированныхОбъектов
        //public IEnumerable<PerformanceItem> МониторПроизводительности()
        //{
        //    //var items = Cache.GetItems();
        //    var list = new List<PerformanceItem>();
        //    //var times = items.Sum(p => (float)(p.Value.AvgTime ?? 0f));
        //    //var counts = items.Sum(p => (float)(p.Value.Count));
        //    //var querys = items.Where(p => p.Value is КешЗапрос || p.Value is КешЗапросХешьТаблица).Count();
        //    var domains = СписокДоменов();
        //    foreach (var domain in domains)
        //    {
        //        var allItems = Cache.GetAllForDomain(domain);
        //        var item = new PerformanceItem()
        //        {
        //            domain = domain,
        //            AvgTime = allItems.Sum(p => (long)(p.Value.AvgTime ?? 0L)),
        //            Count = allItems.Sum(p => (long)p.Value.Count),
        //            CountQuery = allItems.Where(p => p.Value is КешЗапрос || p.Value is КешЗапросХешьТаблица).Count(),
        //            КешированноЗначений = MemoryCache.Count(domain),
        //        };
        //        item.PercentTime = (float)item.AvgTime / (float)domains.Count();
        //        item.PercentCount = (float)item.Count / (float)domains.Count();
        //        item.PercentCountQuery = (float)item.CountQuery / (float)domains.Count();
        //        item.Оценка = (double)item.Count / (double)(item.AvgTime > 0 ? item.AvgTime : 1);
        //        list.Add(item);
        //    }
        //    return list;
        //}
        public IEnumerable<CacheObject> СписокКешированныхОбъектов(string user, string domain)
        {
            #region hidden
            //var items = new CacheObject[] 
            //{
            //    new CacheObject()
            //    {
            //        Name = "ЗакешированныхЗначений",
            //        Value = string.Empty,
            //        Type = "Статистика",
            //        Count = new Cache().GetCurrentValuesCount(domain),
            //        Content = "Время очистки 1 минута"
            //    },
            //    new CacheObject()
            //    {
            //        Name = "ЗакешированныхИсторииИзменений",
            //        Value = string.Empty,
            //        Type = "Статистика",
            //        Count = DataClient.HistoryValues.Count(p => p.domain == domain),
            //        Content = "Время очистки 15 минут"
            //    },
            //};
            #endregion

            var allItems = Cache.GetAllForDomain(domain);
            //var times = allItems.Sum(p => (float)(p.Value.AvgTime ?? 0f));
            //var counts = allItems.Sum(p => (float)(p.Value.Count));
            var f = allItems.Select(p =>
                new CacheObject()
                {
                    Name = System.Text.RegularExpressions.Regex.Replace(p.Key, "(.*):", ""),
                    Value = p.Key,
                    //Type = p.Key.Replace(domain+":Z:Кеш", ""),
                    //Type = System.Text.RegularExpressions.Regex.Replace(p.Value.GetType().Name, "^Кеш", ""),
                    //Count = p.Value.Count,
                    //AvgTime = p.Value.AvgTime,
                    Content = p.Value.ToString(),
                    //PercentTime = (float)(p.Value.AvgTime ?? 0f) / times,
                    //PercentCount = (float)p.Value.Count / counts,
                }).ToArray();
            foreach (var item in f)
            {
                var start = item.Value.Replace(domain + ":Z:Кеш", "");
                item.Type = start.Substring(0, start.IndexOf(':'));
            }
            return f;
        }
        public void УдалитьКешированныеОбъекты(string[] items, string user, string domain)
        {
            if (items == null)
            {
                Cache.RemoveAll(domain + ":");
                Cache.RemoveAllResolve(domain);
            }
            else
            {
                foreach (var item in items)
                {
                    if (item.Contains("КешИдентификаторРаздела"))
                    {
                        Cache.RemoveAllResolve(domain);
                    }
                    else if (item.EndsWith("*"))
                    {
                        Cache.RemoveAll(item.Replace("*", ":"));
                    }
                    else
                    {
                        Cache.Remove(item);
                    }
                }
            }
        }
        public void УдалитьКешированныеЗначения(string user, string domain)
        {
            MemoryCache.Clear(domain);
        }
        #endregion

        #region windows services
        public void ЖурналСобытийДобавитьОшибку(string Message, string StackTrace, string user, string domain)
        {
            try
            {
                var values = new Dictionary<string, Value>();
                values.Add("ExceptionMessage", new Value(Message));
                values.Add("ExceptionStackTrace", new Value(StackTrace));
                values.Add("НазваниеОбъекта", new Value(DateTime.Now.ToString()));
                new DataClient().ДобавитьРаздел("ЖурналСобытий", "Exception", values, false, Хранилище.Оперативное, user, domain);
            }
            catch
            {
                try
                {
                    ConfigurationClient.WindowsLog(StackTrace, user, domain, "ЖурналСобытийДобавитьОшибку");
                }
                catch
                {
                }
            }
        }
        public static void WindowsLog(string message, string user, string domain, params object[] arg)
        {
            WindowsLog(message, user, domain, System.Diagnostics.EventLogEntryType.Error, arg);
        }
        public static void WindowsLog(string message, string user, string domain, EventLogEntryType type, params object[] arg)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var source = (domain ?? "") + "@" + (user ?? "");
                    if (!EventLog.SourceExists(source))
                    {
                        EventLog.CreateEventSource(source, "Сервер 5.0");
                    }
                    var sb = new StringBuilder();
                    foreach (var item in arg)
                    {
                        sb.AppendLine((item ?? "").ToString());
                        sb.AppendLine("*************");
                    }
                    sb.Append(message);
                    EventLog.WriteEntry(source, sb.ToString(), type, 1, 1);
                }
                catch (Exception)
                {
                }
            });
        }
        public void Ping()
        {
        }
        #endregion

        public DeleteLog[] ЖурналУдалений(string domain)
        {
            var result = new List<DeleteLog>();
            var filelog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "log_delete.txt");
            if (System.IO.File.Exists(filelog))
            {
                //sbl.AppendFormat("{0,-14}{1,-20}{2,-20}{3,-22}{4}", "Домен", "Пользователь", "Дата", "Тип", "Описание");
                using (var file = new System.IO.StreamReader(filelog))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.StartsWith(domain))
                        {
                            try
                            {
                                result.Add(new DeleteLog()
                                {
                                    user = line.Substring(14, 20).Trim(),
                                    date = Convert.ToDateTime(line.Substring(34, 20).Trim()),
                                    type = line.Substring(54, 22).Trim(),
                                    label = line.Substring(76).Trim(),
                                });
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            return result.OrderByDescending(p => p.date)
                .Take(5000).ToArray();
        }


        public void ComitTransaction()
        {
            MemoryCache.ComitTransactions();
        }
    }
}