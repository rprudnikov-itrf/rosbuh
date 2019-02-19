using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using RosService.Intreface;
using RosService.Helper;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Net;
using System.Data.Odbc;
using System.Xml;
using System.Transactions;
using System.Text;
using RosService.Data;
using RosService.Configuration;
using RosService.Files;
using System.Threading.Tasks;


namespace RosService.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    AddressFilterMode = AddressFilterMode.Any,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false,
    ConfigurationName = "RosService.Services")]
    public partial class ServicesClient : IServices
    {
        protected Data.DataClient data = new Data.DataClient();

        #region e-mail & task
        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        public long Почта_КоличествоПисем(decimal Пользователь, string domain)
        {
            //var client = new Popper.POPClient();
            //try
            //{
            //    client.KeepAliveInterval = 60000;
            //    client.Port = 110;
            //    client.UpdateInterval = 100;
            //    client.Server = "pop.yandex.ru";
            //    client.User = "mrfxnet";
            //    client.Password = "ros123";

            //    if (client.Connect())
            //    {
            //        if (!client.Authenticate())
            //            throw new Exception("Authentication attempt failed");
            //    }
            //    else
            //    {
            //        throw new Exception("Connection attempt failed");
            //    }
            //    return client.CountMessage();
            //}
            //finally
            //{
            //    client.Disconnect();
            //}
            return 0;
        }
        #endregion

        #region Работа с телефонами
        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        public DataTable Данные_СписокТелефонов_Совпадения(string Телефоны, string user, string domain)
        {
            var index = Данные_СписокТелефонов_ПолучитьИндекс(Телефоны);
            if (string.IsNullOrEmpty(index)) return new DataTable("EmptyTable");

            var query = new Query();
            query.ДобавитьПараметр("@Телефоны", index);
            query.Sql = @"
            ;with
	            nodes as (select DISTINCT id_node FROM tblValueString as V INNER JOIN FREETEXTTABLE(tblValueString, [string_value], @Телефоны) as K ON V.id_value = K.[KEY]),
	            НазваниеОбъекта as (select id_node, [string_value] 'value' from tblValueString where [type] = 'НазваниеОбъекта')

            select
	            nodes.id_node,
	            isnull(НазваниеОбъекта.value, '') 'НазваниеОбъекта'
            from
	            nodes
	            left join НазваниеОбъекта on nodes.id_node = НазваниеОбъекта.id_node";
            return data.Поиск(query, Хранилище.Оперативное, domain).Значение;
        }

        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        public void Данные_СписокТелефонов_Проиндекировать(decimal id_node, string user, string domain)
        {
            var phones = new List<object[]>();
            if (id_node == 0)
            {
                var query = new Query();
                query.Sql = @"select * from tblValueString where (@node = [id_node] or @node = 0) and [type] = 'СписокТелефонов.Телефоны' and [string_value] <> ''";
                query.ДобавитьПараметр("@node", id_node);
                phones.AddRange(data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Select(
                    p => new object[] { p.Field<decimal>("id_node"), p.Field<string>("string_value") }));
            }
            else
            {
                phones.Add(new object[] { id_node, data.ПолучитьЗначение<object>(id_node, "СписокТелефонов.Телефоны", Хранилище.Оперативное, domain) });
            }

            foreach (var item in phones)
            {
                var numbers = Regex.Replace(Convert.ToString(item[1]), @"[^\d]", string.Empty);
                var items = new List<string>();
                data.СохранитьЗначениеПростое((decimal)item[0], "СписокТелефонов.ТелефоныИндекс", Данные_СписокТелефонов_ПолучитьИндекс(Convert.ToString(item[1])), false, Хранилище.Оперативное, user, domain);
                data.СохранитьЗначениеПростое((decimal)item[0], "СписокТелефонов.ТелефоныЦифры", numbers, false, Хранилище.Оперативное, user, domain);
            }
        }
        public string Данные_СписокТелефонов_ПолучитьИндекс(string Телефоны)
        {
            var items = new List<string>();
            foreach (var p in Regex.Matches(Convert.ToString(Телефоны), @"([\d-(). ]+)+").Cast<Match>())
            {
                var phone = Regex.Replace(p.ToString(), @"[^\d]", string.Empty);
                if (phone.Length < 6) continue;

                if(phone.Length >= 7)
                    items.Add(phone.Substring(phone.Length - 7, 7));
                else
                    items.Add(phone.Substring(phone.Length - 6, 6));
            }
            return string.Join(" ", items.ToArray());
        }
        #endregion

        #region Банки
        public БанкСведения[] ПоискБанка(string БИК)
        {
            //SELECT        VKEY, `REAL`, PZN, UER, RGN, IND, TNP, NNP, ADR, RKC, NAMEP, NAMEN, NEWNUM, NEWKS, PERMFO, SROK, AT1, AT2, TELEF, REGN, OKPO, DT_IZM, 
            //              CKS, KSNP, DATE_IN, DATE_CH, VKEYDEL
            //FROM            bnkseek
            //where NEWNUM = @bik

            var list = new List<БанкСведения>();
            var stringBuilder = new OdbcConnectionStringBuilder(System.Configuration.ConfigurationManager.ConnectionStrings["БазаДанныхБанков"].ConnectionString)
            {
                Driver = "Microsoft dBASE Driver (*.dbf)"
            };
            using (var connection = new OdbcConnection(stringBuilder.ToString()))
            {
                var command = connection.CreateCommand();
                command.CommandText = string.Format("SELECT * FROM bnkseek.dbf WHERE NEWNUM = '{0}'", БИК);
                command.CommandType = CommandType.Text;

                try
                {
                    if (((command.Connection.State & System.Data.ConnectionState.Open) != System.Data.ConnectionState.Open))
                        command.Connection.Open();

                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new БанкСведения()
                            {
                                Название = Convert.ToString(reader["NAMEP"]),
                                КорСчет = Convert.ToString(reader["KSNP"]),
                                БИК = Convert.ToString(reader["NEWNUM"]),
                                Адрес = Convert.ToString(reader["ADR"]),
                                Город = Convert.ToString(reader["NNP"])
                            });
                        }
                    }
                }
                finally
                {
                    if ((command.Connection.State == System.Data.ConnectionState.Closed))
                    {
                        command.Connection.Close();
                    }
                }
            }
            return list.ToArray();
        }
        #endregion

        #region Адреса
        public string[] ПоискАдреса(string Адрес, int Количество)
        {
            try
            {
                #region service
                //var host = @"http://kladr.itrf.ru";
                //var binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
                //binding.MaxReceivedMessageSize = int.MaxValue;
                //binding.ReaderQuotas.MaxArrayLength = int.MaxValue;

                //var endpoint = new EndpointAddress(new Uri(host));
                //using (var kladr = new ServiceKladr.Service1Client(binding, endpoint))
                //{
                //    return kladr.ПоискАдреса(Адрес, Количество);
                //}
                #endregion

                var list = new List<string>();
                using (SqlConnection conn = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["БазаКладр"].ConnectionString))
                using (SqlCommand comm = conn.CreateCommand())
                {
                    var read = null as SqlDataReader;
                    try
                    {
                        comm.CommandText = @"
SELECT 
	FT_TBL.ADDRESS
FROM dbo.SEARCH AS FT_TBL 
    INNER JOIN CONTAINSTABLE(dbo.SEARCH, ADDRESS, 
		@text 
		, LANGUAGE 'Russian', @top) 
        AS KEY_TBL ON FT_TBL.ID_ADRESS = KEY_TBL.[KEY];";

                        var text = "";
                        var s = Адрес.Trim().Split(' ');

                        foreach (var t in s)
                        {
                            text += (!string.IsNullOrEmpty(text) ? " AND " : "") + string.Format("\"{0}*\"", t.Trim());
                        }

                        comm.Parameters.AddWithValue("@text", text);
                        comm.Parameters.AddWithValue("@top", Количество);

                        conn.Open();
                        read = comm.ExecuteReader();

                        while (read.Read())
                        {
                            list.Add(Convert.ToString(read[0]));
                        }
                    }
                    finally
                    {
                        if (read != null)
                            read.Close();
                        if (conn != null)
                            conn.Close();
                    }
                }

                return list.ToArray();
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("Произошла ошибка в КЛАДР: \n\n\n" + ex.ToString(), "", "@кладр");
            }
            return new string[0];
        }
        public string[] ПоискКоординатГеокодирование(string Адрес)
        {
            try
            {
                var url = string.Format(@"http://maps.google.com/maps/geo?q={1}&output=xml&oe=utf8&sensor=false&key={0}",
                    System.Configuration.ConfigurationManager.AppSettings["GoogleMapKey"],
                    Адрес);

                var webclient = new WebClient();
                var xml = new XmlDocument();
                xml.LoadXml(webclient.DownloadString(url));
                var node = xml["kml"]["Response"]["Placemark"]["Point"]["coordinates"];
                if (node != null)
                {
                    return node.InnerText.Split(',');
                }
            }
            catch
            {
            }
            return new string[] { "0.0", "0.0"};
        }
        #endregion

        #region Сообщения & Задачи пользовател
        public IEnumerable<СведенияПользователя> Пользователи_Список(string user, string domain)
        {
            //return new СведенияПользователя[0];

            var query = new Query();
            //query.CacheName = "@@ПользователиВСети";
            //query.CacheLocation = Query.OutputCacheLocation.Memory;
            //query.CacheDuration = TimeSpan.FromSeconds(60);
            query.Типы.Add("Пользователь%");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "РазрешитьВход", Значение = true });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "cast((case when (ВремяСессии.[value] is null) then 0 when (ВремяСессии.[value] >= getdate()) then 1 else 0 end) as bit) 'Online'", Функция = Query.ФункцияАгрегации.Sql });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "ВремяСессии" });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "НазваниеОбъекта" });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "СсылкаНаГруппуПользователей" });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "СсылкаНаАватар" });
            return data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                .Select(p => new СведенияПользователя()
                {
                    id_node = p.Field<decimal>("id_node"),
                    Группа = string.IsNullOrEmpty(p.Field<string>("СсылкаНаГруппуПользователей.НазваниеОбъекта")) ? p.Field<string>("type") : p.Field<string>("СсылкаНаГруппуПользователей.НазваниеОбъекта"),
                    НазваниеОбъекта = p.Field<string>("НазваниеОбъекта"),
                    ВСети = p.Field<bool>("Online"),
                    СсылкаНаАватар = p.Field<decimal>("СсылкаНаАватар") == 0m ? null as decimal? : p.Field<decimal>("СсылкаНаАватар")
                });
        }

        private Query СообщенияПользователяЗапрос()
        {
            var query = new Query();
            query.CacheName = "СообщениеПользователя";
            query.ДобавитьТипы("СообщениеПользователя");
            query.ДобавитьВыводимыеКолонки("ДатаСозданияОбъекта", "СсылкаНаПользователя", "@Новый", "СсылкаНаОбъект", "ИдентификаторОбъекта");
            query.ДобавитьВыводимыеКолонки("ТекстСообщения").ПолнотекстовыйВывод = true;
            return query;
        }

        public static object lockAddUser = new System.Object();
        public void СообщенияПользователя_Добавить(object ОтКогоЛогинПользователя, object[] КомуЛогинПользователя, string Сообщение, string user, string domain)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                lock (lockAddUser)
                {
                    try
                    {
                        var ОтКого = ОтКогоЛогинПользователя is string ? НайтиПользователяПоЛогину((string)ОтКогоЛогинПользователя, domain).id_node : Convert.ToDecimal(ОтКогоЛогинПользователя);
                        if (ОтКого == 0m) throw new Exception("ОтКогоЛогинПользователя не найден");

                        foreach (var item in КомуЛогинПользователя)
                        {
                            var Кому = item is string ? НайтиПользователяПоЛогину((string)item, domain).id_node : Convert.ToDecimal(item);
                            if (Кому == 0m) continue;

                            var values = new Dictionary<string, Value>();
                            values.Add("СсылкаНаПользователя", new Value(ОтКого));
                            values.Add("ТекстСообщения", new Value(Сообщение));
                            values.Add("СсылкаНаОбъект", new Value(Кому));
                            values.Add("ИдентификаторОбъекта", new Value(string.Format("MSG_{0:F0}_{1:F0}", ОтКого, Кому)));
                            values.Add("@Новый", new Value(ОтКого != Кому));
                            data.ДобавитьРаздел(Кому, "СообщениеПользователя", values, false, Хранилище.Оперативное, user, domain);

                            //отправить сообщение на почту если чел не в сети.
                            if (data.ПолучитьЗначение<DateTime>(Кому, "ВремяСессии", Хранилище.Оперативное, domain) < DateTime.Now)
                            {
                                var Email = data.ПолучитьЗначение<string>(Кому, "Email", Хранилище.Оперативное, domain);
                                if (!string.IsNullOrEmpty(Email))
                                {
                                    data.ОтправитьПисьмо(Email,
                                        string.Format("{0} :сообщение от {1}", domain, data.ПолучитьЗначение<string>(ОтКого, "НазваниеОбъекта", Хранилище.Оперативное, domain)),
                                        string.Format("{0}\n\n-------\nРосИнфоТех\nwww.itrf.ru\n", Сообщение),
                                        null, false, user, domain);
                                }
                            }
                        }

                        System.Threading.Thread.Sleep(10);
                    }
                    catch (Exception ex)
                    {
                        new ConfigurationClient().ЖурналСобытийДобавитьОшибку("Ошибка 'Services.СообщенияПользователя_Добавить'", ex.ToString(), user, domain);
                    }
                }
            });
        }
        public void СообщенияПользователя_Очистить(decimal СсылкаНаПользователя, decimal СсылкаНаОбъект, string user, string domain)
        {
            var query = СообщенияПользователяЗапрос();
            //query.Типы.Add("СообщениеПользователя");
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаПользователя", Значение = СсылкаНаОбъект });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаОбъект", Значение = СсылкаНаПользователя });
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "@Новый", Значение = true });
            //query.ДобавитьМестоПоиска(СсылкаНаПользователя, 1);
            foreach (var item in data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable())
            {
                data.СохранитьЗначениеПростое(item.Field<decimal>("id_node"), "@Новый", false, 
                    false, Хранилище.Оперативное,
                    user, domain);
            }
        }
        public IEnumerable<СообщенияПользователя> СообщенияПользователя_Список(decimal СсылкаНаПользователя, string user, string domain)
        {
            try
            {
                var query = СообщенияПользователяЗапрос();
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "@Новый", Значение = true });
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаОбъект", Значение = СсылкаНаПользователя });

                //query.СтрокаЗапрос = "[Группировки=(СсылкаНаПользователя)]";
                //query.Типы.Add("СообщениеПользователя%");
                //query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "count(*) 'Counts'", Функция = Query.ФункцияАгрегации.Sql });
                //query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "СсылкаНаПользователя" });
                //query.ДобавитьМестоПоиска(СсылкаНаПользователя, 1);
                return data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                    .GroupBy(p => p.Field<decimal>("СсылкаНаПользователя"))
                    .Select(p => new СообщенияПользователя() { id_node = p.Key, Количество = p.Count() });
            }
            catch(Exception)
            {
                return Enumerable.Empty<СообщенияПользователя>();
            }
        }


        public IEnumerable<ЗадачиПользователя> ЗадачаПользователя_Список(decimal СсылкаНаПользователя, string user, string domain)
        {
            try
            {
                var query = new Query();
                query.СтрокаЗапрос = "[Группировки=(СсылкаНаОбъект)]";
                query.Типы.Add("СлужебнаяЗадача%");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаПользователя", Значение = СсылкаНаПользователя });
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаОбъект", Значение = СсылкаНаПользователя, Оператор = Query.Оператор.НеРавно });
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "Статус", Значение = "В работе" });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "count(*) 'Counts'", Функция = Query.ФункцияАгрегации.Sql });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "@Новый", Функция = Query.ФункцияАгрегации.Sum });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "Срочно", Функция = Query.ФункцияАгрегации.Sum });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "СсылкаНаОбъект" });
                var tasks1 = data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                    .Select(p => new ЗадачиПользователя()
                    {
                        id_node = p.Field<decimal>("СсылкаНаОбъект"),
                        Количество = p.Field<int>("Counts"),
                        Новые = Convert.ToInt32(p["@Новый"]),
                        Срочные = Convert.ToInt32(p["Срочно"])
                    });

                query = new Query();
                query.Типы.Add("СлужебнаяЗадача%");
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаОбъект", Значение = СсылкаНаПользователя });
                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "Статус", Значение = "В работе" });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "count(*) 'Counts'", Функция = Query.ФункцияАгрегации.Sql });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "@Новый", Функция = Query.ФункцияАгрегации.Sum });
                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "Срочно", Функция = Query.ФункцияАгрегации.Sum });
                return data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                    .Select(p => new ЗадачиПользователя()
                    {
                        id_node = СсылкаНаПользователя,
                        Количество = p.Field<int>("Counts"),
                        Новые = Convert.ToInt32(p["@Новый"]),
                        Срочные = Convert.ToInt32(p["Срочно"])
                    })
                    .Union(tasks1);
            }
            catch
            {
                return Enumerable.Empty<ЗадачиПользователя>();
            }
        }
        public void ЗадачаПользователя_Добавить(object ОтКогоЛогинПользователя, object[] КомуЛогинПользователя, string Сообщение, bool Срочно, object Срок, Dictionary<string, byte[]> Файлы, string user, string domain)
        {
            try
            {
                var ОтКого = ОтКогоЛогинПользователя is string ? НайтиПользователяПоЛогину((string)ОтКогоЛогинПользователя, domain).id_node : Convert.ToDecimal(ОтКогоЛогинПользователя);
                foreach (var item in КомуЛогинПользователя)
                {
                    var Кому = item is string ? НайтиПользователяПоЛогину((string)item, domain).id_node : Convert.ToDecimal(item);

                    var values = new Dictionary<string, Value>();
                    values.Add("СсылкаНаПользователя", new Value(ОтКого));
                    values.Add("НазваниеОбъекта", new Value(Сообщение.Length > 50 ? Сообщение.Substring(0, 50) : Сообщение));
                    //values.Add("Содержание", new Value(Сообщение));
                    values.Add("СсылкаНаОбъект", new Value(Кому));
                    values.Add("Срочно", new Value(Срочно));
                    values.Add("Срок", new Value(Срок ?? DateTime.Now));
                    values.Add("Статус", new Value("В работе"));
                    values.Add("ИдентификаторОбъекта", new Value(string.Format("TASK_{0:F0}_{1:F0}", ОтКого, Кому)));
                    values.Add("@Новый", new Value(true));
                    values.Add("Вложения", new Value(Файлы != null && Файлы.Count > 0));
                    var id_task = data.ДобавитьРаздел(Кому, "СлужебнаяЗадача", values, false, Хранилище.Оперативное, user, domain);
                    if (Файлы != null)
                    {
                        foreach (var f in Файлы)
                        {
                            new FileClient().СохранитьФайлПолностью(id_task, null, f.Key, null, f.Value, Хранилище.Оперативное, user, domain);
                        }
                    }

                    #region Сообщение
                    values = new Dictionary<string, Value>();
                    values.Add("СсылкаНаПользователя", new Value(ОтКого));
                    values.Add("ТекстСообщения", new Value(Сообщение));
                    values.Add("СсылкаНаОбъект", new Value(id_task));
                    values.Add("ИдентификаторОбъекта", new Value(string.Format("MSG_{0:F0}_{1:F0}", ОтКого, id_task)));
                    //values.Add("@Новый", new Value(ОтКого != Кому));
                    data.ДобавитьРаздел(id_task, "СообщениеПользователя", values, false, Хранилище.Оперативное, user, domain);
                    #endregion

                    //отправить сообщение на почту если чел не в сети.
                    if (data.ПолучитьЗначение<DateTime>(Кому, "ВремяСессии", Хранилище.Оперативное, domain) < DateTime.Now)
                    {
                        var Email = data.ПолучитьЗначение<string>(Кому, "Email", Хранилище.Оперативное, domain);
                        if (!string.IsNullOrEmpty(Email))
                        {
                            data.ОтправитьПисьмо(Email,
                                string.Format("{0} :новая задача от {1}", domain, data.ПолучитьЗначение<string>(ОтКого, "НазваниеОбъекта", Хранилище.Оперативное, domain)),
                                string.Format("{0}\n\n-------\nРосИнфоТех\nwww.itrf.ru\n", Сообщение),
                                Файлы != null ? Файлы.Select(p => new Файл() { Name = p.Key, Stream = p.Value }).ToArray() : null as Файл[],
                                Срочно, false, user, domain);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку("Ошибка 'Services.ЗадачаПользователя_Добавить'", ex.ToString(), user, domain);
            }
        }

        public Пользователь НайтиПользователяПоЛогину(string login, string domain)
        {
            //var query = new Query() { КоличествоВыводимыхДанных = 1, КоличествоВыводимыхСтраниц = 1 };
            //query.ДобавитьТипы("Пользователь%");
            //query.ДобавитьУсловиеПоиска("ЛогинПользователя", login);
            //return data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Single().Field<decimal>("id_node"); 

            var query = new Query();
            query.CacheName = "__FullTextSearch";
            query.Sql = "select [id_node], [string_value_index] 'ЛогинПользователя' from tblValueString WITH(NOLOCK) where /*[hide] = 0 and*/ [string_value_index] = @ЛогинПользователя and [type] = 'ЛогинПользователя'";
            query.ДобавитьПараметр("@ЛогинПользователя", login);
            var Пользователь = null as EnumerableRowCollection<DataRow>;
            try
            {
                Пользователь = data.Поиск(query, Хранилище.Оперативное, domain).Значение.AsEnumerable();
            }
            catch (Exception ex)
            {
                throw new Exception("Не верно указан домен.", ex);
            }

            if (Пользователь.Count() > 1) 
                throw new Exception("В системе задано более одного пользователя с указанным логином, свяжитесь с технической поддержкой.");
            if (Пользователь.Count() == 0)
                return new Пользователь() { id_node = 0, ЛогинПользователя = string.Empty }; 

            var user = Пользователь.First();
            return new Пользователь() { id_node = user.Field<decimal>("id_node"), ЛогинПользователя = user.Field<string>("ЛогинПользователя") };
        }
        public class Пользователь
        {
            public decimal id_node { get; set; }
            public string ЛогинПользователя { get; set; }
        }
        #endregion

        #region Рассылки
        public void Рассылка(string Тема, string Содержание, object ПапкаАдресатами, Dictionary<string, byte[]> Файлы, string user, string domain)
        {
            var oRow = 0;
            try
            {
                var query = new Query();
                query.ДобавитьУсловиеПоиска("Email", "", Query.Оператор.НеРавно);
                query.ДобавитьВыводимыеКолонки("Email");
                query.ДобавитьМестоПоиска(ПапкаАдресатами, 0);
                var t = data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Select(p => p.Field<string>("Email")).Distinct();
                foreach (var item in t)
                {
                    data.ОтправитьПисьмо(item, Тема, Содержание,
                        Файлы.Select(p => new Файл() { Name = p.Key, Stream = p.Value }).ToArray(),
                        false, user, domain);
                }
                oRow += t.Count();

                query = new Query();
                query.ДобавитьУсловиеПоиска("Контакты.Email", "", Query.Оператор.НеРавно);
                query.ДобавитьВыводимыеКолонки("Контакты.Email");
                query.ДобавитьМестоПоиска(ПапкаАдресатами, 0);
                t = data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Select(p => p.Field<string>("Контакты.Email")).Distinct();
                foreach (var item in t)
                {
                    data.ОтправитьПисьмо(item, Тема, Содержание,
                        Файлы.Select(p => new Файл() { Name = p.Key, Stream = p.Value }).ToArray(),
                        false, user, domain);
                }
                oRow += t.Count();

                СообщенияПользователя_Добавить("ЛокальнаяСистема", new object[] { user },
                    string.Format("Рассылка почты завершена, отправлено {0} сообщений.", oRow), user, domain);
            }
            catch(Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку("Ошибка при рассылке почты 'Services.Рассылка'", ex.ToString(), user, domain);
                СообщенияПользователя_Добавить("ЛокальнаяСистема", new object[] { user },
                    string.Format("Ошибка при рассылке почты, отправлено {0} сообщений. Обратитесь в службу технической поддержки", oRow), user, domain);
            }
        }
        #endregion

        #region Статистика
        //private static Dictionary<string, object> Статистика;
        //private static DateTime ВремяЖизниСтатистики;
        private static readonly object lockStaticObject = new System.Object();
        public Dictionary<string, object> СтатистикаКонфигурации(string user, string domain)
        {
            var result = new Dictionary<string, object>();
            result["ВсегоПользователей"] = 0;
            result["ВсегоПользователейВСети"] = 0;
            return result;


            //if(Статистика == null)
            //    Статистика = new Dictionary<string, object>();

            //if (ВремяЖизниСтатистики < DateTime.Now)
            //{
            //    lock (lockStaticObject)
            //    {
            //        try
            //        {
            //            using (var w = new WebClient())
            //            {
            //                Статистика["ВсегоПользователей"] = Convert.ToInt32(w.DownloadString("http://g.itrf.ru/v.ashx?key=Регистрации::Всего"));
            //                Статистика["ВсегоПользователейВСети"] = Convert.ToInt32(w.DownloadString("http://g.itrf.ru/v.ashx?key=Сессия*"));
            //            }

            //            //using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            //            //using (var ds = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            //            //{
            //            //    var domains = new ConfigurationClient().СписокДоменов();
            //            //    var sql = new StringBuilder();
            //            //    //всего пользователей
            //            //    sql.AppendLine("select SUM(C) from (");
            //            //    sql.AppendLine(string.Join("union",
            //            //        //domains.Select(p => string.Format("\tselect COUNT(*) 'C' from [{0}].[dbo].[tblNode] WITH(NOLOCK) where [type] = 'Пользователь'", p)).ToArray()));
            //            //        domains.Select(p => string.Format("\tselect COUNT(*) 'C' from [{0}].[dbo].[tblValue] WITH(NOLOCK) where [type] = 'ЛогинПользователя' and [string_value_index] <> ''", p)).ToArray()));
            //            //    sql.AppendLine(") as t");

            //            //    //пользователей в сети
            //            //    sql.AppendLine("select SUM(C) from (");
            //            //    sql.AppendLine(string.Join("union",
            //            //        domains.Select(p => string.Format("\tselect COUNT(*) 'C' from [{0}].[dbo].[tblValue] WITH(NOLOCK) where [type] = 'ВремяСессии' and [datetime_value] >= GETDATE()", p)).ToArray()));
            //            //    sql.AppendLine(") as t");

            //            //    var command = (db.Connection as SqlConnection).CreateCommand();
            //            //    command.CommandText = sql.ToString();
            //            //    command.CommandTimeout = 300;
            //            //    using (var adapter = new SqlDataAdapter(command))
            //            //    {
            //            //        adapter.AcceptChangesDuringFill = false;
            //            //        adapter.AcceptChangesDuringUpdate = false;
            //            //        adapter.Fill(ds);

            //            //        if (ds.Tables.Count > 0)
            //            //        {
            //            //            Статистика["ВсегоПользователей"] = ds.Tables[0].Rows[0][0];
            //            //            Статистика["ВсегоПользователейВСети"] = ds.Tables[1].Rows[0][0];
            //            //        }
            //            //    }
            //            //}
            //        }
            //        catch(Exception ex)
            //        {
            //            ConfigurationClient.Domains = null;
            //            ConfigurationClient.WindowsLog("СтатистикаКонфигурации", user, domain, ex.ToString());
            //            return null;
            //        }
            //        ВремяЖизниСтатистики = DateTime.Now.AddSeconds(30);
            //    }
            //}
            //return Статистика;
        }

        public void ОптравитьВСтатистику(string Приложение, string Источник, DateTime Дата, decimal Значение)
        {
            if (Значение == 0) 
                return;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var paramList = new Dictionary<string, object>();
                    paramList["Application"] = Приложение;
                    paramList["Source"] = Источник;
                    paramList["Date"] = Дата.ToString("d");
                    paramList["Value"] = Значение.ToString();

                    var serverUrl = "http://g.itrf.ru/set.ashx";

                    System.Net.WebRequest req = System.Net.WebRequest.Create(serverUrl);
                    req.Method = "POST";
                    req.ContentType = "application/x-www-form-urlencoded";
                    req.Timeout = 30 * 1000;

                    var query = string.Join("&", paramList.Select(p => string.Format("{0}={1}", p.Key, p.Value)).ToArray());

                    byte[] requestBodyBytes = System.Text.Encoding.UTF8.GetBytes(query);

                    req.ContentLength = requestBodyBytes.Length;

                    System.IO.Stream newStream = req.GetRequestStream();
                    newStream.Write(requestBodyBytes, 0, requestBodyBytes.Length);
                    newStream.Close();

                    System.Net.WebResponse response = req.GetResponse();

                    System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream());
                    reader.ReadToEnd();
                }
                catch { }
            });
        }
        #endregion
    }
}
