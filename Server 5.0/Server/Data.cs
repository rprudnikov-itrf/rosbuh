using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RosService.Caching;
using RosService.Configuration;
using RosService.Intreface;

namespace RosService.Data
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    AddressFilterMode = AddressFilterMode.Any,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false,
    ConfigurationName = "RosService.Data")]
    public partial class DataClient : IData
    {
        public const string CONNECTOR_CACHE_PREFIX = ".cache";

        private static bool? _ОтключитьУдаление;
        public static bool ОтключитьУдаление
        {
            get
            {
                if (_ОтключитьУдаление == null)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьУдаление"] == null)
                        _ОтключитьУдаление = false;
                    else
                        _ОтключитьУдаление = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьУдаление"]);
                }
                return _ОтключитьУдаление.Value;
            }
        }

        #region Service
        internal static string GetTablePrefix(Хранилище хранилище)
        {
            switch (хранилище)
            {
                case Хранилище.Оперативное:
                    return string.Empty;

                case Хранилище.Конфигурация:
                    return "assembly_";

            }
            throw new Exception("Таблица не найдена.");
        }
        internal static string GetTableValue(MemberTypes membertypes)
        {
            switch (membertypes)
            {
                case MemberTypes.String:
                    return "tblValueString";
                case MemberTypes.Int:
                case MemberTypes.Double:
                    return "tblValueDouble";
                case MemberTypes.DateTime:
                    return "tblValueDate";
                case MemberTypes.Bool:
                    return "tblValueBool";
                case MemberTypes.Ссылка:
                    return "tblValueHref";
                case MemberTypes.Byte:
                    return "tblValueByte";
                default:
                    throw new Exception(string.Format("GetTableValue: не возможно определить имя таблицы для типа '{0}'", membertypes.ToString()));
            }
        }
        #endregion

        #region Основные
        public DependencyNodeInfo[] СписокЗависимыхРазделов(object id_node, Хранилище хранилище, string domain)
        {
            var __id_node = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
            if (__id_node == 0) return null;

            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                var command = (db.Connection as SqlConnection).CreateCommand();
                command.Parameters.AddWithValue("@id_node", __id_node).SqlDbType = SqlDbType.Decimal;
                command.CommandText = string.Format(@"
                    set nocount on
                    ------------------
                    declare @HashCode varchar(900);
                    select @HashCode = [HashCode] + '%' from {0}tblNode WITH(NOLOCK) where [id_node] = @id_node;

                    ;with
	                    links as (select * from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [TypeHashCode] like '00007%'),
	                    val as (select [id_node],[type],[double_value] from {0}tblValueHref WITH(NOLOCK) where [hide] = 0 and [double_value] = @id_node),
	                    nodes as (select [id_node],[type] from {0}tblNode WITH(NOLOCK) where [hide] = 0 and [HashCode] not like @HashCode and @HashCode not like [HashCode] + '%'),
	                    НазваниеОбъекта as (select [id_node], [string_value_index] from {0}tblValueString WITH(NOLOCK) where [type] = 'НазваниеОбъекта')

                    select
	                    nodes.id_node 'id_node',
	                    nodes.type 'Тип',
	                    isnull(НазваниеОбъекта.[string_value_index],'<не указано>') 'НазваниеОбъекта',
	                    links.Name 'Атрибут',
                        Группа = 'На объект ссылаются'
                    	
                    from
	                    links WITH(NOLOCK)
	                    inner join val WITH(NOLOCK) on val.[type] = links.[Name]
	                    inner join nodes WITH(NOLOCK) on nodes.[id_node] = val.[id_node]
	                    left join НазваниеОбъекта WITH(NOLOCK) on nodes.[id_node] = НазваниеОбъекта.[id_node]",
                GetTablePrefix(хранилище));
                //order by
                //Тип, 
                //НазваниеОбъекта

                using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                using (var adapter = new SqlDataAdapter(command))
                {
                    if (adapter.Fill(table) > 0)
                    {
                        return table.Tables[0].AsEnumerable().Select(p => new DependencyNodeInfo()
                        {
                            id_node = p.Field<decimal>("id_node"),
                            Атрибут = p.Field<string>("Атрибут"),
                            Группа = p.Field<string>("Группа"),
                            НазваниеОбъекта = p.Field<string>("НазваниеОбъекта"),
                            Тип = p.Field<string>("Тип")
                        }).ToArray();
                    }
                }
                return new DependencyNodeInfo[0];
            }
        }
        public NodeInfo[] СписокРазделов(decimal id_parent, string Тип, string[] Атрибуты, int limit, Хранилище хранилище, string domain)
        {
            if (limit == 0) limit = int.MaxValue;
            var items = new List<NodeInfo>();
            var СортировкаАтрибут = string.Empty;
            if (id_parent == 0) СортировкаАтрибут = "Сортировка.Позиция";
            else СортировкаАтрибут = ПолучитьЗначение<string>(id_parent, "Сортировка.Атрибут", хранилище, domain);

            if (string.IsNullOrEmpty(СортировкаАтрибут)) СортировкаАтрибут = "НазваниеОбъекта";

            var query = new Query();
            query.КоличествоВыводимыхДанных = limit;
            if (string.IsNullOrEmpty(Тип))
            {
                query.ВыводимыеКолонки.Add(new Query.Колонка()
                {
                    Атрибут = string.Format("Children = (select count(*) from {0}tblNode WITH(NOLOCK) where [hide] = 0 and [id_parent] = nodes.id_node)", GetTablePrefix(хранилище), Тип),
                    Функция = Query.ФункцияАгрегации.Sql
                });
            }
            else
            {
                query.ВыводимыеКолонки.Add(new Query.Колонка()
                {
                    Атрибут = string.Format("Children = (select count(*) from {0}tblNode WITH(NOLOCK) where [hide] = 0 and [type] in (<id:{1}>) and [id_parent] = nodes.id_node)", GetTablePrefix(хранилище), Тип),
                    Функция = Query.ФункцияАгрегации.Sql
                });
            }
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "НазваниеОбъекта" });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "@Новый" });
            query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "HashCode" });
            if (Атрибуты != null)
            {
                query.ВыводимыеКолонки.AddRange(Атрибуты.Select(p => new Query.Колонка() { Атрибут = p }));
            }
            if (!string.IsNullOrEmpty(Тип))
            {
                query.Типы.Add(Тип);
            }
            query.Сортировки.Add(new Query.Сортировка() { Атрибут = СортировкаАтрибут });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = id_parent, МаксимальнаяГлубина = 1 });
            foreach (var p in Поиск(query, хранилище, domain).AsEnumerable())
            {
                var node = new NodeInfo()
                {
                    id_parent = id_parent,
                    id_node = p.Field<decimal>("id_node"),
                    ТипДанных = p.Field<string>("type"),
                    HashCode = p.Field<string>("HashCode"),
                    Описание = p.Field<string>("НазваниеОбъекта"),
                    IsNew = p.Field<bool>("@Новый"),
                    Children = p.Field<int>("Children")
                };
                if (Атрибуты != null && Атрибуты.Length > 0)
                {
                    node.Data = new Dictionary<string, object>();
                    foreach (var a in Атрибуты)
                    {
                        node.Data.Add(a, p[a]);
                    }
                }
                items.Add(node);
            }
            return items.ToArray();
        }
        public NodeInfo ПолучитьРаздел(decimal id_node, string[] Атрибуты, Хранилище хранилище, string domain)
        {
            if (id_node == 0) return null;
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                #region sql
                var node = db.ExecuteQuery<NodeInfo>(string.Format(@"
                    set nocount on
                    ------------------
                    ;with
		                nodes as (select * from {0}tblNode WITH(NOLOCK) where id_node = {{0}}),
		                labels as (select id_node, [string_value] from {0}tblValueString WITH(NOLOCK) where type = 'НазваниеОбъекта' and id_node in(select id_node from nodes WITH(NOLOCK))),
                        is_new as (select id_node, [double_value] from {0}tblValueBool WITH(NOLOCK) where type = '@Новый' and id_node in(select id_node from nodes WITH(NOLOCK)))
                        
	                select
		                nodes.*,
                        nodes.[type] 'ТипДанных',
		                labels.[string_value] 'Описание',
		                isnull(convert(bit, is_new.[double_value]),0) 'IsNew',
		                (select count(id_node) from {0}tblNode WITH(NOLOCK) where HashCode like nodes.HashCode + '_%') 'Children'
	                from
		                nodes WITH(NOLOCK)
		                left join labels WITH(NOLOCK) on nodes.id_node = labels.id_node
                        left join is_new WITH(NOLOCK) on nodes.id_node = is_new.id_node",
                    GetTablePrefix(хранилище), GetTablePrefix(Хранилище.Конфигурация)), id_node).SingleOrDefault();
                #endregion
                if (node != null && Атрибуты != null && Атрибуты.Length > 0)
                {
                    var values = ПолучитьЗначение(id_node, Атрибуты, хранилище, domain);
                    node.Data = new Dictionary<string, object>();
                    foreach (var a in Атрибуты)
                    {
                        node.Data.Add(a, values[a].Значение);
                    }
                }
                return node;
            }
        }

        public decimal AddAsync(object parent, string type, Dictionary<string, object> values, Хранилище stage, string user, string domain)
        {
            return ДобавитьРаздел(parent, type, values.ToDictionary(p => p.Key, e => new Value(e.Value)), false, stage, true, user, domain);
        }
        public decimal Add(object parent, string type, Dictionary<string, object> values, Хранилище stage, string user, string domain)
        {
            return ДобавитьРаздел(parent, type, values.ToDictionary(p => p.Key, e => new Value(e.Value)), false, stage, false, user, domain);
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            return ДобавитьРаздел(id_parent, тип, значения, ДобавитьВИсторию, хранилище, false, user, domain);
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, bool Асинхронно, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return 0;

            if (string.IsNullOrEmpty(тип))
                throw new Exception("Невозможно добавить раздел, не указан 'Тип' раздела");

            var id_node = 0m;
            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_parent, хранилище, domain, false);
            if (значения == null) значения = new Dictionary<string, Value>();


            #region Кешировать сведения о добавлении раздела
            var _hashquery = Cache.Key<КешДобавитьРаздел>(domain, тип);
            var _cache = Cache.Get<КешДобавитьРаздел>(_hashquery);
            if (_cache == null)
            {
                _cache = new КешДобавитьРаздел();
                var ds = new ConfigurationClient().СписокАтрибутов(тип, domain);

                //Сохранить все значения по-умолчанию
                _cache.DefaultValue = ds.Where(p => p.IsSetDefaultValue).ToDictionary(
                    p => p.Name,
                    e => new ConfigurationClient().ПолучитьЗначение(тип, e.Name, domain));

                //Обработать AutoIncrement поля
                _cache.AutoIncrimentValue = ds.Where(p => p.IsAutoIncrement).ToDictionary(
                    p => p.Name,
                    e => e.Name);

                _cache.НазваниеОбъекта = Convert.ToString(new ConfigurationClient().ПолучитьЗначение(тип, "НазваниеОбъекта", domain));
                Cache.Set(_hashquery, _cache);
            }
            #endregion

            try
            {
                if (idParentNode == 0)
                    throw new Exception(string.Format("Не указан родительский раздел #{0}", id_parent));

                var valuesParent = ПолучитьЗначение(idParentNode, new string[] { "HashCode", "НазваниеОбъекта" }, хранилище, domain);
                if (string.IsNullOrEmpty(valuesParent["HashCode"].Значение as string))
                    throw new Exception("Родительский раздел не найден (HashCode = null)");

                using (var db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open)
                            db.Connection.Open();
                        var tablePrefix = GetTablePrefix(хранилище);
                        var command = null as SqlCommand;

                        #region Обработать AutoIncrement поля
                        if (_cache.AutoIncrimentValue != null)
                        {
                            foreach (var attribute in _cache.AutoIncrimentValue)
                            {
                                if (значения.ContainsKey(attribute.Key))
                                    continue;

                                var _hashIncriment = Cache.Key<КешСчётчик>(domain, тип);
                                var value = ПолучитьАвтоматическоеПоле(db, тип, attribute.Value, хранилище, domain);
                                if (value != null)
                                {
                                    //взять счеттчик базового типа
                                    if (value.Значение < 0)
                                    {
                                        value = ПолучитьАвтоматическоеПоле(db,
                                            new ConfigurationClient().ПолучитьТип(тип, domain).BaseType,
                                            attribute.Value, хранилище, domain);
                                    }
                                    value.Значение++;

                                    if (MemoryCache.IsMemoryCacheClient)
                                        Cache.Set(_hashIncriment, value);
                                }
                                значения.Add(attribute.Key, new Value(value.Значение));
                            };
                        }
                        #endregion

                        #region sql добавить раздел
                        command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandTimeout = 600;
                        if (значения != null && значения.ContainsKey("GuidCode"))
                        {
                            command.Parameters.AddWithValue("@GuidCode", System.Data.SqlTypes.SqlGuid.Parse(значения["GuidCode"].Значение as string)).SqlDbType = SqlDbType.UniqueIdentifier;
                            command.CommandText = string.Format(@"
set nocount on
------------------
declare @id_node numeric(18,0);
                        
--создать раздел
begin try

       --создать раздел
       insert into {0}tblNode ([id_parent],[type],[GuidCode]) values (@id_parent,@Name,@GuidCode);
       set @id_node = @@identity;

       --взять ключ родителя
       declare @HashCode varchar(900) = @HashCodeParent + lower(right(convert(varchar, cast(cast(@id_node as bigint) as varbinary),2), 5));
       update {0}tblNode set [HashCode] = @HashCode where [id_node] = @id_node
                   
       --добавить значение @РодительскийРаздел
       insert into {0}tblValueHref ([id_node],[type],[double_value],[string_value_index]) values (@id_node,'@РодительскийРаздел',@id_parent,@LabelParent);

end try
begin catch
select @id_node = [id_node] from tblNode where [GuidCode] = @GuidCode;
end catch
                        
--output
select @id_node", tablePrefix);
                        }
                        else
                        {
                            command.CommandText = string.Format(@"
set nocount on
------------------
declare @id_node numeric(18,0);
                        
--создать раздел
insert into {0}tblNode ([id_parent],[type]) values (@id_parent,@Name);
set @id_node = @@identity;

--взять ключ родителя
declare @HashCode varchar(900) = @HashCodeParent + lower(right(convert(varchar, cast(cast(@id_node as bigint) as varbinary),2), 5));
update {0}tblNode set [HashCode] = @HashCode where [id_node] = @id_node
            
--добавить значение @РодительскийРаздел
insert into {0}tblValueHref ([id_node],[type],[double_value],[string_value_index]) values (@id_node,'@РодительскийРаздел',@id_parent,@LabelParent);
                        
--output
select @id_node", tablePrefix);
                        }
                        command.Parameters.AddWithValue("@id_parent", idParentNode).SqlDbType = SqlDbType.Decimal;
                        command.Parameters.AddWithValue("@Name", тип).SqlDbType = SqlDbType.VarChar;
                        command.Parameters.AddWithValue("@HashCodeParent", valuesParent["HashCode"].Значение).SqlDbType = SqlDbType.VarChar;

                        var str = Convert.ToString(valuesParent["НазваниеОбъекта"].Значение);
                        command.Parameters.AddWithValue("@LabelParent", str.Length > 512 ? str.Substring(0, 512) : str).SqlDbType = SqlDbType.VarChar;
                        id_node = Convert.ToDecimal(command.ExecuteScalar());
                        #endregion

                        //добавить стандартные переменные
                        if (!значения.ContainsKey("ДатаСозданияОбъекта")) значения.Add("ДатаСозданияОбъекта", new Value(DateTime.Now));
                        if (!значения.ContainsKey("РедакторРаздела") && !string.IsNullOrEmpty(user)) значения.Add("РедакторРаздела", new Value(user));

                        #region Сохранить все значения по-умолчанию
                        if (_cache.DefaultValue != null)
                        {
                            foreach (var attribute in _cache.DefaultValue)
                            {
                                if (значения.ContainsKey(attribute.Key)) continue;
                                значения.Add(attribute.Key, new Value(attribute.Value));
                            }
                        }
                        #endregion

                        #region Записать 'НазваниеОбъекта' по шаблону
                        if (!значения.ContainsKey("НазваниеОбъекта") && !string.IsNullOrEmpty(_cache.НазваниеОбъекта))
                        {
                            var НазваниеОбъекта = _cache.НазваниеОбъекта;
                            var matchs = Regex.Matches(НазваниеОбъекта, @"\{(?<Type>([\w/._\-@]+?))(:(?<Format>(\w+?)))?\}", RegexOptions.IgnoreCase).Cast<Match>();
                            //Parallel.ForEach(matchs, (item) =>
                            foreach (var item in matchs)
                            {
                                var value = null as object;
                                if (значения != null && значения.ContainsKey(item.Groups["Type"].Value))
                                    value = значения[item.Groups["Type"].Value].Значение;
                                else
                                    value = ПолучитьЗначение<object>(id_node, item.Groups["Type"].Value, хранилище, domain);

                                if (string.IsNullOrEmpty(item.Groups["Format"].Value))
                                {
                                    НазваниеОбъекта = НазваниеОбъекта.Replace(item.Groups[0].Value, Convert.ToString(value));
                                }
                                else
                                {
                                    НазваниеОбъекта = НазваниеОбъекта.Replace(item.Groups[0].Value,
                                        string.Format("{0:" + item.Groups["Format"].Value + "}", value));
                                }
                            }//);
                            значения.Add("НазваниеОбъекта", new Value(НазваниеОбъекта));
                        }
                        #endregion

                        if (значения != null && значения.ContainsKey("@РодительскийРаздел"))
                            значения.Remove("@РодительскийРаздел");

                        if (Асинхронно)
                        {
                            СохранитьЗначениеАсинхронно(id_node, значения, ДобавитьВИсторию, true, хранилище, user, domain);
                        }
                        else
                        {
                            СохранитьЗначение(id_node, значения, ДобавитьВИсторию, true, хранилище, user, domain, true);
                        }
                    }
                    finally
                    {
                        db.Connection.Close();
                        //ConfigurationClient.WindowsLog("ДобавитьРаздел", user, domain, s.ElapsedMilliseconds.ToString());
                    }
                }

                //s = System.Diagnostics.Stopwatch.StartNew();
                #region Проверить есть ли кеширование данных
                ПроверитьЕстьЛиКешированиеДанных(id_node, тип, значения, true, хранилище, user, domain, idParentNode);
                #endregion

                //ConfigurationClient.WindowsLog("ДобавитьРаздел", user, domain, s.ElapsedMilliseconds.ToString());
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("ДобавитьРаздел", user, domain, тип, ex.ToString());
                throw ex;
            }
            return id_node;
        }

        private КешСчётчик ПолучитьАвтоматическоеПоле(DataClasses.ClientDataContext db, string type, string attribute, Хранилище хранилище, string domain)
        {
            var _hashIncriment = Cache.Key<КешСчётчик>(domain, type);
            var value = null as КешСчётчик;
            lock (КешСчётчик.lockObject)
            {
                value = Cache.Get<КешСчётчик>(_hashIncriment);
                var tablePrefix = GetTablePrefix(хранилище);
                if (value == null)
                {
                    var countDefault = new ConfigurationClient().ПолучитьЗначение<long>(type, attribute, domain);
                    if (countDefault < 0)
                    {
                        value = new КешСчётчик() { Значение = countDefault };
                    }
                    else
                    {
                        //var s = System.Diagnostics.Stopwatch.StartNew();
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandTimeout = 300;
                        command.Parameters.AddWithValue("@type", type).SqlDbType = SqlDbType.VarChar;
                        command.Parameters.AddWithValue("@attr", attribute).SqlDbType = SqlDbType.VarChar;
                        command.CommandText = string.Format(@"declare @id_node decimal(18,0);
                                        select @id_node = max(id_node) from {0}tblNode WITH(NOLOCK) where [type] = @type
                                        select isnull([double_value],0) from {0}tblValueDouble WITH(NOLOCK) where id_node = @id_node and [type] = @attr", tablePrefix);

                        var countCurrent = Convert.ToInt64(command.ExecuteScalar()) + 1;
                        if (countCurrent < countDefault)
                            countCurrent = countDefault;

                        //value = new КешСчётчик() { Значение = countCurrent, AvgTime = (uint)s.ElapsedMilliseconds };
                        value = new КешСчётчик() { Значение = countCurrent };
                    }
                    Cache.Set(_hashIncriment, value);
                }
            }
            return value;
        }

        public void ПереместитьРаздел(object id_node, object ПереместитьВРаздел, bool ОбновитьИндексы, Хранилище хранилище, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            var __id_node = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
            var __ПереместитьВРаздел = RosService.QueryBuilder.ResolveIdNode(ПереместитьВРаздел, хранилище, domain, false);
            if (__id_node == 0m || __ПереместитьВРаздел == 0)
                return;

            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    db.Connection.Open();

                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandTimeout = 180;
                    command.Parameters.AddWithValue("@move", __ПереместитьВРаздел).SqlDbType = SqlDbType.Decimal;
                    command.Parameters.AddWithValue("@id", __id_node).SqlDbType = SqlDbType.Decimal;
                    command.CommandText = string.Format(
                        @"update {0}tblNode set [id_parent] = @move where [id_node] = @id
                      update {0}tblValueHref set [double_value] = @move where [id_node] = @id and [type] = '@РодительскийРаздел'",
                        GetTablePrefix(хранилище));
                    command.ExecuteNonQuery();
                }
                catch
                {
                }
                finally
                {
                    db.Connection.Close();
                }
            }

            if (ОбновитьИндексы)
            {
                //if (!MemoryCache.IsMemoryCacheClient)
                {
                    var key = MemoryCache.Path(domain, хранилище, __id_node);
                    MemoryCache.Remove(key + "HashCode");
                    MemoryCache.Remove(key + "@РодительскийРаздел");
                }

                Проиндексировать(__id_node, хранилище, domain, false);
            }
        }


        #region Значения
        public T ПолучитьЗначение<T>(object id_node, string attribute, Хранилище хранилище, string domain)
        {
            try
            {
                if (typeof(string) == typeof(T)
                    || typeof(decimal) == typeof(T)
                    || typeof(DateTime) == typeof(T)
                    || typeof(int) == typeof(T)
                    || typeof(bool) == typeof(T)
                    || typeof(double) == typeof(T)
                    || typeof(float) == typeof(T)
                    || typeof(byte[]) == typeof(T))
                {
                    var value = ПолучитьЗначениеПростое(id_node, attribute, хранилище, domain);
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
                else
                {
                    var value = ПолучитьЗначение(id_node, new string[] { attribute }, хранилище, domain)[attribute];
                    if (value.IsСписок)
                    {
                        return (T)(object)value.Таблица;
                    }
                    else if (value is T)
                    {
                        return (T)value.Значение;
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
            }
            catch
            {
                return default(T);
            }
        }
        public Dictionary<string, object> Get(object id, string[] keys, Хранилище stage, string domain)
        {
            return ПолучитьЗначение(id, keys, stage, domain).ToDictionary(p => p.Key, e => e.Value.Значение);
        }
        public Dictionary<string, Value> ПолучитьЗначение(object id_node, string[] attributes, Хранилище хранилище, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return new Dictionary<string, Value>();

            if (attributes == null)
                return new Dictionary<string, Value>();

            //Interlocked.Increment(ref Helper.Statistics.Get);

            var ТипРаздела = null as string;
            var values = new ConcurrentDictionary<string, Value>(Environment.ProcessorCount, attributes.Length);
            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
            var __path = MemoryCache.Path(domain, хранилище, idParentNode);

            var attributesNoCache = new List<string>();
            var contaniers = MemoryCache.Get(attributes.Select(p => __path + p).ToArray());
            if (contaniers != null)
            {
                foreach (var attr in attributes)
                {
                    var ___f = __path + attr;
                    if (!contaniers.ContainsKey(___f))
                    {
                        attributesNoCache.Add(attr);
                        continue;
                    }

                    var v = contaniers[___f];
                    values.AddOrUpdate(attr, v.obj, (k, e) => e = v.obj);
                }
            }

            if (attributesNoCache.Count > 0)
            {
                //var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                //Parallel.ForEach(attributesNoCache, options, attribute =>
                foreach (var attribute in attributesNoCache)
                {
                    #region ** or *
                    if (attribute.Equals("**") || attribute.Equals("*"))
                    {
                        if (string.IsNullOrEmpty(ТипРаздела))
                            ТипРаздела = ПолучитьЗначение<string>(idParentNode, "Тип.Имя", хранилище, domain);

                        foreach (var item in ПолучитьЗначение(idParentNode, QueryBuilder.ResolveAttribute(ТипРаздела, attribute, domain).ToArray(), хранилище, domain))
                        {
                            values.AddOrUpdate(item.Key, item.Value, (k, e) => e = item.Value);
                        }
                    }
                    #endregion
                    #region else
                    else
                    {
                        var value = ПолучитьЗначение(idParentNode, attribute, хранилище, domain, __path, ref ТипРаздела);
                        values.AddOrUpdate(attribute, value, (k, e) => e = value);
                    }
                    #endregion
                }//);
            }
            return values.ToDictionary(p => p.Key, e => e.Value);
        }
        public object ПолучитьЗначениеПростое(object id, string attribute, Хранилище stage, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return null;

            //Interlocked.Increment(ref Helper.Statistics.Get);

            var ТипРаздела = null as string;
            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id, stage, domain, false);
            var __path = MemoryCache.Path(domain, stage, idParentNode);
            var contanier = MemoryCache.Get(__path + attribute);
            if (contanier != null)
            {
                return contanier.obj == null ? null : contanier.obj.Значение;
            }

            var value = ПолучитьЗначение(idParentNode, attribute, stage, domain, __path, ref ТипРаздела);
            return value == null ? null : value.Значение;
        }
        private Value ПолучитьЗначение(decimal idParentNode, string attribute, Хранилище хранилище, string domain, string __path, ref string ТипРаздела)
        {
            if (string.IsNullOrEmpty(attribute))
                return null;

            try
            {

                #region получить из кеша
                var __keyfull = __path + attribute;
                var contanier = null as ValueContanier;
                //var contanier = MemoryCache.Get(__keyfull);
                //if (contanier != null)
                //{
                //    return contanier.obj;
                //}
                #endregion

                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    var value = new Value();


                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    try
                    {
                        #region Справочник
                        if (attribute.StartsWith(@"//Справочники/"))
                        {
                            value.IsСписок = true;
                            var query = new Query();
                            query.ДобавитьВыводимыеКолонки(new string[] { "НазваниеОбъекта", "ИдентификаторОбъекта", "ПоУмолчанию" });
                            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = attribute.Split('/').Last(), МаксимальнаяГлубина = 1 });
                            query.Сортировки.Add(new Query.Сортировка() { Атрибут = "НазваниеОбъекта", Направление = Query.НаправлениеСортировки.Asc });
                            value.SetTable(Поиск(query, Хранилище.Оперативное, domain).Значение);
                            //contanier = new ValueContanier() { cache = DateTime.Now.AddHours(3) };
                        }
                        #endregion
                        #region Значение родителя
                        else if (attribute.StartsWith(@"../"))
                        {
                            value.Значение = ПолучитьЗначениеПростое(idParentNode,
                                "../".Equals(attribute) ? "@РодительскийРаздел" : attribute.Replace("../", "@РодительскийРаздел/"),
                                хранилище, domain);

                            contanier = new ValueContanier()
                            {
                                cache = DateTime.Now.Add(MemoryCache.Timeout)
                            };
                        }
                        #endregion
                        #region Поисковый запрос
                        else if (attribute.StartsWith(@"[") && attribute.EndsWith(@"]"))
                        {
                            var query = new Query() { СтрокаЗапрос = attribute };
                            query.Параметры.Add(new Query.Параметр() { Имя = "@id_node", Значение = idParentNode });
                            value.IsСписок = true;
                            value.SetTable(Поиск(query, хранилище, domain).Значение);
                        }
                        #endregion
                        #region Значение по ссылке
                        else if (attribute.Contains(@"/"))
                        {
                            if (idParentNode == 0m)
                            {
                                value.Значение = null;
                                return value;
                            }

                            var path = attribute.Split('/');
                            var currentNode = idParentNode;
                            if (path.Length >= 2 && currentNode > 0)
                            {
                                for (int i = 1; i <= path.Length; i++)
                                {
                                    if (i == path.Length)
                                    {
                                        value.Значение = ПолучитьЗначение<object>(currentNode, path[i - 1], хранилище, domain);
                                        break;
                                    }
                                    currentNode = ПолучитьЗначение<decimal>(currentNode, path[i - 1], хранилище, domain);

                                    //если ссылка не задана остановить поиск
                                    if (currentNode == 0)
                                        break;
                                }
                            }
                        }
                        #endregion
                        #region Системный атрибут
                        else if (attribute.Equals("HashCode") || attribute.Equals("@HashCode"))
                        {
                            if (idParentNode == 0m)
                            {
                                value.Значение = string.Empty;
                                return value;
                            }

                            var command = (db.Connection as SqlConnection).CreateCommand();
                            command.CommandText = string.Format("set nocount on; select [HashCode] from {0}tblNode WITH(NOLOCK) where id_node = @id_node", GetTablePrefix(хранилище));
                            command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                            value.Значение = (string)command.ExecuteScalar();

                            contanier = new ValueContanier()
                            {
                                cache = DateTime.Now.Add(MemoryCache.Timeout)
                            };
                        }
                        else if (attribute.Equals("GuidCode") || attribute.Equals("@GuidCode"))
                        {
                            if (idParentNode == 0m)
                            {
                                value.Значение = string.Empty;
                                return value;
                            }

                            var command = (db.Connection as SqlConnection).CreateCommand();
                            command.CommandText = string.Format("set nocount on; select [GuidCode] from {0}tblNode WITH(NOLOCK) where id_node = @id_node", GetTablePrefix(хранилище));
                            command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                            value.Значение = Convert.ToString(command.ExecuteScalar());

                            contanier = new ValueContanier()
                            {
                                cache = DateTime.Now.Add(MemoryCache.Timeout)
                            };
                        }
                        else if (attribute.Equals("Тип.Имя"))
                        {
                            if (string.IsNullOrEmpty(ТипРаздела))
                                ТипРаздела = ПолучитьТипРаздела(idParentNode, хранилище, domain);
                            value.Значение = ТипРаздела;

                            contanier = new ValueContanier()
                            {
                                cache = DateTime.Now.Add(MemoryCache.Timeout)
                            };
                        }
                        else if (attribute.Equals("@@КоличествоФайлов"))
                        {
                            value.Значение = СписокФайлов(idParentNode, хранилище, domain).Count();
                            contanier = new ValueContanier()
                            {
                                cache = DateTime.Now.Add(MemoryCache.Timeout)
                            };
                        }
                        else if (attribute.Equals("id_node"))
                        {
                            value.Значение = idParentNode;
                        }
                        #endregion
                        #region Значение
                        else
                        {
                            var type = new ConfigurationClient().ПолучитьТип(attribute, domain);
                            if (type == null)
                                type = new Configuration.Type() { Name = attribute, RegisterType = RegisterTypes.string_value, MemberType = MemberTypes.String };

                            if (type.IsReadOnly)
                            {
                                if (idParentNode > 0 && string.IsNullOrEmpty(ТипРаздела))
                                    ТипРаздела = ПолучитьЗначение<string>(idParentNode, "Тип.Имя", хранилище, domain);
                                value.Значение = new ConfigurationClient().ПолучитьЗначение(ТипРаздела, attribute, domain);
                                contanier = new ValueContanier()
                                {
                                    cache = DateTime.Now.Add(MemoryCache.Timeout)
                                };
                            }
                            else if (idParentNode > 0m)
                            {
                                var command = (db.Connection as SqlConnection).CreateCommand();
                                command.CommandTimeout = 120;
                                #region sql
                                var converter = string.Empty;
                                switch (type.MemberType)
                                {
                                    case MemberTypes.Double:
                                        converter = @"isnull([double_value],0)";
                                        break;

                                    case MemberTypes.Ссылка:
                                        converter = @"isnull(convert(numeric(18,0), [double_value]),0)";
                                        break;

                                    case MemberTypes.Int:
                                        converter = @"isnull(convert(int, [double_value]),0)";
                                        break;

                                    case MemberTypes.Bool:
                                        converter = @"isnull(convert(bit, [double_value]),0)";
                                        break;

                                    case MemberTypes.DateTime:
                                        converter = @"[datetime_value]";
                                        break;

                                    case MemberTypes.Byte:
                                        converter = @"[byte_value]";
                                        break;

                                    default:
                                        converter = @"isnull([string_value],'')";
                                        break;
                                }
                                switch (type.MemberType)
                                {
                                    case MemberTypes.Bool:
                                        {
                                            command.CommandText = "set nocount on; declare @value bit = 0; select @value = " +
                                                converter + " from " + GetTablePrefix(хранилище) + "tblValueBool WITH(NOLOCK) where [id_node] = @id_node and [type] = @type; select @value";
                                        }
                                        break;

                                    default:
                                        {
                                            command.CommandText = @"set nocount on; select " + converter + " from " + GetTablePrefix(хранилище) + GetTableValue(type.MemberType) + " WITH(NOLOCK) where [id_node] = @id_node and [type] = @type";
                                        }
                                        break;
                                }
                                #endregion
                                command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                                command.Parameters.AddWithValue("@type", type.Name).SqlDbType = SqlDbType.VarChar;

                                //сохранить массив значений int[], string[], ...
                                if (attribute.EndsWith("[]"))
                                {
                                    var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false };
                                    new SqlDataAdapter(command).Fill(table);
                                    value.IsСписок = true;
                                    table.Tables[0].TableName = "Values";
                                    value.SetTable(table.Tables[0]);
                                }
                                //сохранить тип данных 'таблица'
                                else if (type.MemberType == MemberTypes.Таблица)
                                {
                                    //получить поисковый запрос из конфигурации
                                    if (string.IsNullOrEmpty(ТипРаздела))
                                        ТипРаздела = ПолучитьЗначение<string>(idParentNode, "Тип.Имя", хранилище, domain);

                                    var query = new Query() { СтрокаЗапрос = new ConfigurationClient().ПолучитьЗначение(ТипРаздела, attribute, domain) as string };
                                    query.Параметры.Add(new Query.Параметр() { Имя = "@id_node", Значение = idParentNode });
                                    value.IsСписок = true;
                                    value.SetTable(Поиск(query, хранилище, domain).Значение);
                                    value.Значение = null;
                                }
                                else
                                {
                                    value.Значение = command.ExecuteScalar();
                                    contanier = new ValueContanier()
                                    {
                                        cache = DateTime.Now.Add(MemoryCache.Timeout)
                                    };
                                }
                            }

                            //проставить значение по-умолчанию
                            if (Convert.IsDBNull(value.Значение) || value.Значение == null)
                            {
                                switch (type.MemberType)
                                {
                                    case MemberTypes.String:
                                        value.Значение = string.Empty;
                                        break;
                                    case MemberTypes.Int:
                                        value.Значение = default(int);
                                        break;
                                    case MemberTypes.Ссылка:
                                    case MemberTypes.Double:
                                        value.Значение = default(decimal);
                                        break;
                                    case MemberTypes.Bool:
                                        value.Значение = default(bool);
                                        break;
                                }
                            }
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        ConfigurationClient.WindowsLog(ex.ToString(), хранилище.ToString(), domain, attribute);
                    }
                    finally
                    {
                        if (db.Connection != null)
                            db.Connection.Close();

                        if (Convert.IsDBNull(value.Значение))
                            value.Значение = null;

                        #region сохранить в кеш
                        if (contanier != null)
                        {
                            contanier.obj = value;
                            MemoryCache.Set(__keyfull, contanier);
                        }
                        #endregion
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), хранилище.ToString(), domain, attribute);
            }
            return null;
        }

        public Dictionary<string, Value> ПолучитьЗначенияФормы(string Шаблон, decimal id_node, Хранилище хранилище, string domain)
        {
            //var timer = System.Diagnostics.Stopwatch.StartNew();
            var attributes = new string[0];
            if (id_node > 0)
            {
                if (string.IsNullOrEmpty(Шаблон))
                {
                    Шаблон = ПолучитьЗначение<string>(id_node, "Тип.Имя", хранилище, domain); //ПолучитьТипРаздела(id_node, хранилище, domain);
                }
                attributes = new string[] { "НазваниеОбъекта", /*"@@КоличествоФайлов",*/ "GuidCode" };
            }

            if (string.IsNullOrEmpty(Шаблон))
                throw new Exception(string.Format("Шаблон '{1}' для раздела №{0:f0} не найден", id_node, Шаблон));

            #region Получить список атрибутов
            var binders = new ConfigurationClient().Binder_СписокСвязей(Шаблон, domain).Select(p => p.attribute);
            #endregion

            var values = ПолучитьЗначение(id_node, attributes.Union(binders).ToArray(), хранилище, domain);

            #region Проверить значения по-умолчанию
            if (!values.ContainsKey("Тип.Имя") || string.IsNullOrEmpty(values["Тип.Имя"].Значение as string))
            {
                values["Тип.Имя"] = new Value(Шаблон);
            }
            if (id_node == 0 && (!values.ContainsKey("Тип.Описание") || string.IsNullOrEmpty(values["Тип.Описание"].Значение as string)))
            {
                values["Тип.Описание"] = new Value(new ConfigurationClient().ПолучитьЗначение(Шаблон, "Тип.Описание", domain));
            }
            #endregion

            #region @ВремяПодготовкиДанных
            //if (!values.ContainsKey("@ВремяПодготовкиДанных"))
            //{
            //    timer.Stop();
            //    values.Add("@ВремяПодготовкиДанных", new Value(timer.ElapsedMilliseconds));
            //}
            values.Add("@ВремяПодготовкиДанных", new Value(0));
            #endregion

            return values;
        }
        public object ПолучитьКонстанту(string Имя, string domain)
        {
            return ПолучитьЗначение<object>("&НазваниеОбъекта=" + Имя, "ЗначениеКонстанты", Хранилище.Оперативное, domain);
            //using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            //{
            //    try
            //    {
            //        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
            //        var command = (db.Connection as SqlConnection).CreateCommand();
            //        command.CommandText = string.Format(@"select [id_node] from tblValueString WITH(NOLOCK) where [type] = 'НазваниеОбъекта' and [string_value_index] = @name");
            //        command.Parameters.AddWithValue("@name", Имя).SqlDbType = SqlDbType.VarChar;
            //        return ПолучитьЗначение<object>(Convert.ToDecimal(command.ExecuteScalar()), "ЗначениеКонстанты", Хранилище.Оперативное, domain);
            //    }
            //    finally
            //    {
            //        db.Connection.Close();
            //    }
            //}
        }

        public void SetAsync(object id, Dictionary<string, object> values, Хранилище stage, string user, string domain)
        {
            СохранитьЗначениеАсинхронно(id, values.ToDictionary(p => p.Key, e => new Value(e.Value)), false, false, stage, user, domain);
        }
        public void Set(object id, Dictionary<string, object> values, Хранилище stage, string user, string domain)
        {
            СохранитьЗначение(id, values.ToDictionary(p => p.Key, e => new Value(e.Value)), false, false, stage, user, domain, true);
        }
        public void СохранитьЗначениеПоиск(Query запрос, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            var Формулы = false;
            var Замены = new Dictionary<string, string>();
            var result = Поиск(запрос, хранилище, domain);
            foreach (var item in значения)
            {
                if (item.Value.Значение is string && ((string)item.Value.Значение).StartsWith("="))
                {
                    var column = ((string)item.Value.Значение).Substring(1);
                    if (result.Значение.Columns.Contains(column))
                    {
                        Замены.Add(item.Key, column);
                        if (!Формулы) Формулы = true;
                    }
                }
            }
            foreach (var item in result.Значение.AsEnumerable())
            {
                //если указано значение =Атрибу и тарибут есть в поиске то заменить на значение атрибута
                if (Формулы)
                {
                    foreach (var r in Замены)
                        значения[r.Key].Значение = item[r.Value];
                }
                СохранитьЗначение(item.Field<decimal>("id_node"), значения, ДобавитьВИсторию, хранилище, user, domain);
            }
        }
        public void СохранитьЗначение(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            СохранитьЗначение(id_node, значения, ДобавитьВИсторию, false, хранилище, user, domain, true);
        }
        internal void СохранитьЗначение(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, bool IsNew, Хранилище хранилище, string user, string domain, bool isValidMemoryCache)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            if (значения == null)
                return;

            //Interlocked.Increment(ref Helper.Statistics.Set);

            try
            {
                var Дата = DateTime.Now;
                var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
                if (idParentNode == 0m)
                    return;

                var __path = MemoryCache.Path(domain, хранилище, idParentNode);
                //var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                //Parallel.ForEach(значения, options, item =>
                foreach (var item in значения)
                {
                    СохранитьЗначение(idParentNode, item.Key, item.Value, ДобавитьВИсторию, IsNew, хранилище, user, domain, __path, Дата, 0, isValidMemoryCache);
                }//);


                #region Проверить есть ли кеширование данных
                if (!IsNew)
                {
                    ПроверитьЕстьЛиКешированиеДанных(idParentNode, ПолучитьЗначение<string>(idParentNode, "Тип.Имя", хранилище, domain), значения, false, хранилище, user, domain, 0);
                }
                #endregion
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "СохранитьЗначение*", "id_node: " + id_node.ToString());
            }
        }

        private static object lockLog = new System.Object();
        private void СохранитьЗначение(decimal idParentNode, string attribute, Value value, bool ДобавитьВИсторию, bool IsNew, Хранилище хранилище, string user, string domain, string __path, DateTime Дата, int trynumber, bool isValidMemoryCache)
        {
            //не сохранять историю если выбрано не оперативное хранилище
            //if (хранилище != Хранилище.Оперативное && ДобавитьВИсторию)
            //    ДобавитьВИсторию = false;

            var __value = null as object;
            var __command = null as SqlCommand;
            var Prefix = GetTablePrefix(хранилище);
            using (var db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();


                    #region Array
                    if (attribute.EndsWith("[]"))
                    {
                        var table = null as DataTable;
                        var type = new ConfigurationClient().ПолучитьТип(attribute, domain);
                        if (type == null)
                            return; //continue;

                        var buffer = value.Таблица;
                        if (buffer != null && buffer.Columns.Count > 0)
                        {
                            table = buffer;
                        }
                        else
                        {
                            table = new DataTable();
                            table.Columns.Add("Value", typeof(object));
                            table.Rows.Add(new object[] { value.Значение ?? Convert.DBNull });
                        }

                        if (table != null)
                        {
                            #region если в списке есть Null тоочистить список
                            var sb = null as StringBuilder;
                            if (table.AsEnumerable().Count(p => p[0] == null || Convert.IsDBNull(p[0])) > 0)
                            {
                                sb = new StringBuilder();
                                sb.AppendLine("set nocount on;");
                                sb.AppendLine("delete from " + Prefix + GetTableValue(type.MemberType) + " where [id_node] = @id_node and [type] = @type");

                                __command = (db.Connection as SqlConnection).CreateCommand();
                                __command.CommandTimeout = 600;
                                __command.CommandText = sb.ToString();
                                __command.Parameters.AddWithValue("@type", attribute).SqlDbType = SqlDbType.VarChar;
                                __command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                                __command.ExecuteNonQuery();
                            }
                            #endregion

                            #region sql
                            sb = new StringBuilder(200);
                            sb.AppendLine("set nocount on;");
                            sb.AppendLine("insert into " + Prefix + GetTableValue(type.MemberType) + " ([id_node],[type],[" + type.RegisterType + "]) values (@id_node,@HashCode,@value);");
                            #endregion

                            foreach (DataRow row in table.Rows)
                            {
                                if (row[0] == null || Convert.IsDBNull(row[0])) continue;

                                __command = (db.Connection as SqlConnection).CreateCommand();
                                __command.CommandTimeout = 600;
                                __command.CommandText = sb.ToString();
                                __command.Parameters.AddWithValue("@HashCode", attribute).SqlDbType = SqlDbType.VarChar;
                                RosService.QueryBuilder.AddWithValue(__command, "@value", row[0]);
                                __command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                                __command.ExecuteNonQuery();

                                //if (ДобавитьВИсторию && !Cache.ОтключитьАрхивЗначений)
                                //{
                                //    Cache.HistoryValues.Enqueue(new RosService.Caching.HistoryValueContanier()
                                //    {
                                //        date = Дата,
                                //        domain = domain,
                                //        user = user,
                                //        хранилище = хранилище,
                                //        attribute = attribute,
                                //        registerType = type.RegisterType,
                                //        id_node = idParentNode,
                                //        value = row[0]
                                //    });
                                //}
                            }
                        }
                    }
                    #endregion
                    #region Поисковые таблицы
                    else if (value.IsСписок)
                    {
                        var table = value.Таблица;
                        var IsIdNode = table.Columns.Contains("id_node");
                        var IsType = table.Columns.Contains("type");
                        var __IsNew = table.Columns.Contains("@@IsNew");
                        if (!IsIdNode && !IsType)
                            throw new Exception("Для сохранения таблицы необходима хотя бы одна колонка [id_node] или [type]");

                        //удалить разделы, которые возможно были удалены
                        var current_values = ПолучитьЗначение<DataTable>(idParentNode, attribute, хранилище, domain);
                        if (current_values != null)
                        {
                            foreach (DataRow row in current_values.Rows)
                            {
                                if (table == null || table.AsEnumerable().FirstOrDefault(p => p["id_node"].Equals(row["id_node"])) == null)
                                {
                                    УдалитьРаздел(false, false, new decimal[] { row.Field<decimal>("id_node") }, хранилище, user, domain);
                                }
                            }
                        }
                        var values = null as Dictionary<string, Value>;
                        if (table != null)
                        {
                            foreach (var row in table.AsEnumerable())
                            {
                                values = new Dictionary<string, Value>();
                                foreach (DataColumn column in table.Columns)
                                {
                                    if (column.ColumnName == "id_node"
                                        || column.ColumnName == "type"
                                        || column.ColumnName == "@@IsNew"
                                        || column.ColumnName == "НомерСтроки")
                                        continue;

                                    if (column.ColumnName.EndsWith(".НазваниеОбъекта"))
                                        continue;

                                    if (column.ColumnName.Contains("/"))
                                        continue;

                                    values.Add(column.ColumnName, new Value(row[column.ColumnName]));
                                }

                                //если в таблице есть атрибут @@IsNew то добавить строчку как обязательную
                                if (__IsNew && IsType && row["@@IsNew"].Equals(true))
                                    ДобавитьРаздел(idParentNode, row.Field<string>("type"), values, false, хранилище, user, domain);
                                //обновить раздел
                                else if (IsIdNode && row.Field<decimal>("id_node") > 0)
                                    СохранитьЗначение(row.Field<decimal>("id_node"), values, false, хранилище, user, domain);
                                //добавить новый раздел
                                else if (IsType)
                                    ДобавитьРаздел(idParentNode, row.Field<string>("type"), values, false, хранилище, user, domain);
                            }
                        }
                    }
                    #endregion
                    #region Значение по ссылке
                    else if (attribute.Contains(@"/"))
                    {
                        if (idParentNode == 0m)
                            return;

                        var path = attribute.Split('/');
                        var currentNode = idParentNode;
                        if (path.Length >= 2 && currentNode > 0)
                        {
                            for (int i = 1; i <= path.Length; i++)
                            {
                                if (i == path.Length)
                                {
                                    var __currentNodepath = MemoryCache.Path(domain, хранилище, currentNode);
                                    СохранитьЗначение(currentNode, path[i - 1], value, ДобавитьВИсторию, IsNew, хранилище, user, domain, __currentNodepath, Дата, 0, isValidMemoryCache);
                                    break;
                                }
                                currentNode = ПолучитьЗначение<decimal>(currentNode, path[i - 1], хранилище, domain);

                                //если ссылка не задана остановить поиск
                                if (currentNode == 0)
                                    break;
                            }
                        }
                    }
                    #endregion
                    #region GuidCode
                    else if (attribute == "GuidCode")
                    {
                        var guid = value.Значение as string;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            var contanier = new ValueContanier() { obj = new Value(guid), cache = DateTime.Now.Add(MemoryCache.Timeout) };
                            MemoryCache.Set(__path + attribute, contanier);

                            __command = (db.Connection as SqlConnection).CreateCommand();
                            __command.CommandTimeout = 600;
                            __command.CommandText = string.Format("update {0}tblNode set [GuidCode] = @GuidCode where [id_node] = @id_node", Prefix);
                            __command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                            __command.Parameters.AddWithValue("@GuidCode", guid).SqlDbType = SqlDbType.VarChar;
                            __command.ExecuteNonQuery();

                            //удалить в памяти
                            var __cachePathNode = Cache.KeyResolve(domain, "&" + guid);
                            Cache.SetResolve(__cachePathNode, new RosService.Caching.КешИдентификаторРаздела()
                            {
                                id_node = idParentNode
                            });

                            //ConfigurationClient.WindowsLog("DEBUG", user, domain, "id_node = " + idParentNode.ToString(),
                            //    "set value = " + value.Значение.ToString(),
                            //    "connector = " + db.Connection.ConnectionString); 
                        }
                    }
                    #endregion
                    #region Простейшее значение
                    else
                    {
                        var type = new ConfigurationClient().ПолучитьТип(attribute, domain);
                        if (type == null)
                        {
                            #region Создать тип
                            try
                            {
                                var __base = "string";
                                if (value.Значение is decimal
                                    || value.Значение is float
                                    || value.Значение is double)
                                {
                                    __base = "double";
                                }
                                else if (value.Значение is int)
                                {
                                    __base = "int";
                                }
                                else if (value.Значение is byte[])
                                {
                                    __base = "byte";
                                }
                                else if (value.Значение is DateTime)
                                {
                                    __base = "datetime";
                                }
                                var __type = new ConfigurationClient().ДобавитьТип(0, attribute, attribute, "System.Default", __base, false, true, user, domain);
                                new ConfigurationClient().ДобавитьАтрибут(ПолучитьТипРаздела(idParentNode, хранилище, domain), __type, true, user, domain);

                                type = new ConfigurationClient().ПолучитьТип(attribute, domain);
                                if (type == null)
                                    throw new Exception();
                            }
                            catch (Exception)
                            {
                                type = new Configuration.Type() { RegisterType = RegisterTypes.string_value, MemberType = MemberTypes.String };
                            }
                            #endregion
                        }

                        __command = (db.Connection as SqlConnection).CreateCommand();

                        #region sql
                        var sb = new StringBuilder(200);
                        sb.AppendLine("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
                        sb.AppendLine("SET NOCOUNT ON;");
                        switch (type.RegisterType)
                        {
                            #region double_value
                            case RegisterTypes.double_value:
                                if (type.MemberType == MemberTypes.Ссылка)
                                {
                                    if (!IsNew)
                                    {
                                        sb.AppendLine("update " + Prefix + "tblValueHref set [double_value] = @value, [string_value_index] = (select [string_value_index] from " + Prefix + "tblValueString WITH(NOLOCK) where [id_node] = @value and [type] = 'НазваниеОбъекта') where id_node = @id_node and [type] = @HashCode;");
                                        sb.Append("if(@@Rowcount = 0) ");
                                    }
                                    sb.AppendLine("insert into " + Prefix + "tblValueHref ([id_node],[type],[double_value],[string_value_index]) values (@id_node,@HashCode,@value,(select [string_value_index] from " + Prefix + "tblValueString WITH(NOLOCK) where [id_node] = @value and [type] = 'НазваниеОбъекта'));");
                                }
                                else
                                {
                                    if (!IsNew)
                                    {
                                        sb.AppendLine("update " + Prefix + GetTableValue(type.MemberType) + " set [double_value] = @value where [id_node] = @id_node and [type] = @HashCode;");
                                        sb.Append("if(@@Rowcount = 0) ");
                                    }
                                    sb.AppendLine("insert into " + Prefix + GetTableValue(type.MemberType) + " ([id_node],[type],[double_value]) values (@id_node,@HashCode,@value);");
                                }

                                if (value.Значение is string)
                                {
                                    try
                                    {
                                        var str = Convert.ToString(value.Значение).Replace(".", ",");
                                        switch (type.MemberType)
                                        {
                                            case MemberTypes.Ссылка:
                                                __value = QueryBuilder.ResolveIdNode(str, хранилище, domain, false);
                                                break;

                                            case MemberTypes.Int:
                                            case MemberTypes.Double:
                                                try
                                                {
                                                    __value = Convert.ToDecimal(str);
                                                }
                                                catch (InvalidCastException)
                                                {
                                                    __value = Convert.ToDecimal(Regex.Replace(str.TrimEnd(new char[] { ',' }), "[^\\d,]", ""));
                                                }
                                                catch (FormatException)
                                                {
                                                    __value = Convert.ToDecimal(Regex.Replace(str.TrimEnd(new char[] { ',' }), "[^\\d,]", ""));
                                                }
                                                break;

                                            case MemberTypes.Bool:
                                                __value = Convert.ToBoolean(str);
                                                break;
                                        }
                                    }
                                    catch
                                    {
                                        __value = null;
                                    }
                                }
                                else if (value.Значение != null)
                                {
                                    try
                                    {
                                        switch (type.MemberType)
                                        {
                                            case MemberTypes.Int:
                                            case MemberTypes.Double:
                                            case MemberTypes.Ссылка:
                                                __value = Convert.ToDecimal(value.Значение);
                                                break;

                                            case MemberTypes.Bool:
                                                __value = Convert.ToBoolean(value.Значение);
                                                break;
                                        }
                                    }
                                    catch
                                    {
                                        __value = null;
                                    }
                                }
                                __command.Parameters.AddWithValue("@value", __value ?? Convert.DBNull).SqlDbType = SqlDbType.Decimal;
                                break;
                            #endregion

                            #region datetime_value
                            case RegisterTypes.datetime_value:
                                if (!IsNew)
                                {
                                    sb.AppendLine("update " + Prefix + "tblValueDate set [datetime_value] = @value where id_node = @id_node and [type] = @HashCode;");
                                    sb.Append("if(@@Rowcount = 0) ");
                                }
                                sb.AppendLine("insert into " + Prefix + "tblValueDate ([id_node],[type],[datetime_value]) values (@id_node,@HashCode,@value);");

                                if (value.Значение != null)
                                {
                                    try
                                    {
                                        __value = Convert.ToDateTime(value.Значение);
                                        if (DateTime.MinValue.Equals(__value))
                                            __value = null;
                                        else if (DateTime.MaxValue.Equals(__value))
                                            __value = null;
                                    }
                                    catch
                                    {
                                        __value = null;
                                    }
                                }
                                __command.Parameters.AddWithValue("@value", __value ?? Convert.DBNull).SqlDbType = SqlDbType.DateTime;
                                break;
                            #endregion

                            #region byte_value
                            case RegisterTypes.byte_value:
                                if (!IsNew)
                                {
                                    sb.AppendLine("update " + Prefix + "tblValueByte set [byte_value] = convert(varbinary(max),@value) where id_node = @id_node and [type] = @HashCode;");
                                    sb.AppendLine("if(@@Rowcount = 0) ");
                                }
                                sb.AppendLine("insert into " + Prefix + "tblValueByte ([id_node],[type],[byte_value]) values (@id_node,@HashCode,convert(varbinary(max),@value));");

                                //if (ДобавитьВИсторию)
                                //    sb.Append("insert into tblHistoryValue ([date],[storage],[id_node],[type],[user],[byte_value]) values (@date,@storage,@id_node,@HashCode,@user,convert(varbinary(max),@value));\n");

                                if (value.Значение != null)
                                {
                                    try
                                    {
                                        __value = value.Значение;
                                    }
                                    catch
                                    {
                                        __value = null;
                                    }
                                }
                                __command.Parameters.AddWithValue("@value", __value ?? Convert.DBNull).SqlDbType = SqlDbType.VarBinary;
                                break;
                            #endregion

                            #region string_value
                            case RegisterTypes.string_value:
                            default:
                                if (!IsNew)
                                {
                                    sb.AppendLine("update " + Prefix + "tblValueString set [string_value] = @value, [string_value_index] = @value_string_index where id_node = @id_node and [type] = @HashCode;");
                                    sb.Append("if(@@Rowcount = 0) ");
                                }
                                sb.AppendLine("insert into " + Prefix + "tblValueString ([id_node],[type],[string_value],[string_value_index]) values (@id_node,@HashCode,@value,@value_string_index);");


                                if (attribute == "НазваниеОбъекта" && !IsNew)
                                {
                                    sb.AppendFormat(@"
--обновить ссылки у НазваниеОбъекта
update {0}tblValueHref set 
[string_value_index] = @value_string_index
from 
{0}tblValueHref
inner join assembly_tblAttribute A on {0}tblValueHref.[type] = A.[Name]
where 
{0}tblValueHref.[double_value] = @id_node and (A.[id_parent] = 0 and A.[MemberType] = 7)
", Prefix);
                                }

                                //добавить индекс
                                if (value.Значение == null)
                                {
                                    __command.Parameters.AddWithValue("@value_string_index", Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                }
                                else
                                {
                                    var str = Convert.ToString(value.Значение).Trim();
                                    __command.Parameters.AddWithValue("@value_string_index", str.Length > 512 ? str.Substring(0, 512) : str).SqlDbType = SqlDbType.VarChar;
                                    __value = str;
                                }
                                __command.Parameters.AddWithValue("@value", Convert.ToString(__value) ?? Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                break;
                            #endregion
                        }
                        #endregion

                        #region Проверить изменение значения
                        if (isValidMemoryCache)
                        {
                            var contanierCurrent = MemoryCache.Get(__path + attribute);
                            if (contanierCurrent != null && contanierCurrent.obj != null
                                && contanierCurrent.obj.Значение == __value)
                            {
                                return;
                            }
                        }
                        #endregion

                        #region Выполнить действия для системных атрибутов
                        if (value != null && value.Значение != null)
                        {
                            switch (attribute)
                            {
                                case "@РодительскийРаздел":
                                case "../":
                                    ПереместитьРаздел(idParentNode, Convert.ToDecimal(value.Значение), true, хранилище, domain);
                                    break;

                                case "ИдентификаторОбъекта":
                                    var __cachePathNode = RosService.Caching.Cache.KeyResolve(domain, Convert.ToString(value.Значение));
                                    var __КешИдентификаторРаздела = Cache.GetResolve(__cachePathNode);
                                    if (__КешИдентификаторРаздела != null)
                                    {
                                        Cache.RemoveResolve(__cachePathNode);
                                    }
                                    break;

                                case "Тип.Имя":
                                    ИзменитьТипРаздела(idParentNode, Convert.ToString(value.Значение), хранилище, domain);
                                    break;
                            }
                        }
                        #endregion

                        #region Сохранить & Кешировать значение
                        Parallel.Invoke(
                            delegate()
                            {
                                var contanier = new ValueContanier() { obj = new Value(__value), cache = DateTime.Now.Add(MemoryCache.Timeout) };
                                MemoryCache.Set(__path + attribute, contanier);
                            },
                            delegate()
                            {
                                if (sb.Length > 0)
                                {
                                    __command.CommandTimeout = 600;
                                    __command.CommandText = sb.ToString();
                                    __command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                                    __command.Parameters.AddWithValue("@HashCode", attribute).SqlDbType = SqlDbType.VarChar;
                                    __command.ExecuteNonQuery();
                                }
                            });
                        #endregion

                        #region ДобавитьВИсторию
                        //if (ДобавитьВИсторию && !Cache.ОтключитьАрхивЗначений)
                        //{
                        //    Cache.HistoryValues.Enqueue(new RosService.Caching.HistoryValueContanier()
                        //    {
                        //        date = Дата,
                        //        domain = domain,
                        //        user = user,
                        //        хранилище = хранилище,
                        //        attribute = attribute,
                        //        registerType = type.RegisterType,
                        //        id_node = idParentNode,
                        //        value = __value
                        //    });
                        //}
                        #endregion
                    }
                    #endregion
                }
                catch (SqlException ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "СохранитьЗначение - Sql", attribute, value.Значение, "id_node: " + idParentNode.ToString());
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "СохранитьЗначение", attribute, value.Значение, "id_node: " + idParentNode.ToString(),
                        __command != null ? __command.CommandText : string.Empty);
                }
                finally
                {
                    if (db.Connection != null)
                    {
                        db.Connection.Close();
                        db.Connection.Dispose();
                    }
                    __value = null;
                    __command = null;
                }
            }
        }
        public void СохранитьЗначениеПростое(object id, string attribute, object value, bool history, Хранилище stage, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            if ((0m).Equals(id))
                return;

            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id, stage, domain, false);
            var __path = MemoryCache.Path(domain, stage, idParentNode);
            var __value = new Value(value);

            Parallel.Invoke(
                delegate()
                {
                    СохранитьЗначение(idParentNode, attribute, __value, history, false, stage, user, domain, __path, DateTime.Now, 0, true);
                },
                delegate()
                {
                    ПроверитьЕстьЛиКешированиеДанных(idParentNode,
                        ПолучитьЗначение<string>(idParentNode, "Тип.Имя", stage, domain),
                        new Dictionary<string, Value>() { { attribute, __value } },
                        false, stage, user, domain, 0);
                });
        }

        private void ПроверитьЕстьЛиКешированиеДанных(decimal id_node, string ТипРаздела, Dictionary<string, Value> значения, bool IsNew, Хранилище хранилище, string user, string domain, decimal id_parent)
        {
            //var options = new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(1, Convert.ToInt32((float)Environment.ProcessorCount / 2.0f)) };
            var СписокЗависимыхТаблиц = Cache.СписокЗависимыхТаблиц(ТипРаздела, хранилище, domain);
            if (IsNew)
            {
                //Parallel.ForEach(СписокЗависимыхТаблиц, options, item =>
                foreach (var item in СписокЗависимыхТаблиц)
                {
                    using (var db = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX, item.Таблица.Substring(6)))
                    {
                        #region Добавить в таблицу
                        var attrs = new List<string>(item.ЗависимыеАтрибуты.Length + 10);
                        var vals = new List<string>(item.ЗависимыеАтрибуты.Length + 10);
                        var __command = (db.Connection as SqlConnection).CreateCommand();

                        try
                        {
                            if (db.Connection.State != ConnectionState.Open)
                                db.Connection.Open();

                            var isUnique = new HashSet<string>();
                            foreach (var p in item.ЗависимыеАтрибуты)
                            {
                                if (isUnique.Contains(p.Атрибут))
                                    continue;

                                isUnique.Add(p.Атрибут);
                                var value = null as Value;
                                if (!значения.TryGetValue(p.Атрибут, out value)
                                    && (p.Атрибут.Contains("/")
                                        || p.Атрибут.Equals("HashCode") || p.Атрибут.Equals("@HashCode")
                                        || p.Атрибут.Equals("GuidCode") || p.Атрибут.Equals("@GuidCode")))
                                {
                                    //resolve value /НазваниеОбъекта
                                    value = new Value(ПолучитьЗначение<object>(id_node, p.Атрибут, хранилище, domain));
                                }
                                else if (p.Атрибут == "@РодительскийРаздел")
                                {
                                    value = new Value(id_parent);
                                }

                                #region set default value
                                if (value == null)
                                    value = new Value();
                                if (value.Значение == null)
                                {
                                    value.Значение = QueryBuilder.DefaultValue(p.MemberType);
                                }
                                #endregion

                                var parseName = "@" + QueryColumn.ParseName(p.Атрибут);
                                var parseAttr = QueryColumn.ParseAttrInsert(p.Атрибут);
                                try
                                {
                                    switch (p.MemberType)
                                    {
                                        case MemberTypes.Ссылка:
                                            __command.Parameters.AddWithValue(parseName, QueryBuilder.ResolveIdNode(value.Значение, хранилище, domain, false)).SqlDbType = SqlDbType.Decimal;
                                            break;

                                        case MemberTypes.Int:
                                        case MemberTypes.Double:
                                            {
                                                var __value = 0m;
                                                try
                                                {
                                                    __value = Convert.ToDecimal(value.Значение);
                                                }
                                                catch (InvalidCastException)
                                                {
                                                    __value = Convert.ToDecimal(Regex.Replace(value.Значение.ToString().TrimEnd(new char[] { ',' }), "[^\\d,]", ""));
                                                }
                                                catch (FormatException)
                                                {
                                                    __value = Convert.ToDecimal(Regex.Replace(value.Значение.ToString().TrimEnd(new char[] { ',' }), "[^\\d,]", ""));
                                                }
                                                __command.Parameters.AddWithValue(parseName, __value).SqlDbType = SqlDbType.Decimal;
                                            }
                                            break;

                                        case MemberTypes.DateTime:
                                            {
                                                var __value = null as object;
                                                if (value.Значение != null)
                                                {
                                                    try
                                                    {
                                                        __value = Convert.ToDateTime(value.Значение);
                                                        if (DateTime.MinValue.Equals(__value))
                                                            __value = null;
                                                        else if (DateTime.MaxValue.Equals(__value))
                                                            __value = null;
                                                    }
                                                    catch
                                                    {
                                                        __value = null;
                                                    }
                                                }
                                                __command.Parameters.AddWithValue(parseName, __value ?? Convert.DBNull).SqlDbType = SqlDbType.DateTime;
                                            }
                                            break;

                                        case MemberTypes.Bool:
                                            __command.Parameters.AddWithValue(parseName, (value.Значение is string) ? Convert.ToBoolean(value.Значение) : value.Значение ?? false).SqlDbType = SqlDbType.Bit;
                                            break;

                                        case MemberTypes.Byte:
                                            __command.Parameters.AddWithValue(parseName, value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.VarBinary;
                                            break;

                                        default:
                                            if (p.ПолнотекстовыйВывод)
                                            {
                                                __command.Parameters.AddWithValue(parseName, value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                            }
                                            else
                                            {
                                                if (value.Значение == null)
                                                    __command.Parameters.AddWithValue(parseName, Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                                else if (value.Значение is string)
                                                    __command.Parameters.AddWithValue(parseName, ((string)value.Значение).Length > 512 ? ((string)value.Значение).Substring(0, 512) : (string)value.Значение).SqlDbType = SqlDbType.VarChar;
                                                else
                                                    __command.Parameters.AddWithValue(parseName, value.Значение).SqlDbType = SqlDbType.VarChar;
                                            }
                                            break;
                                    }
                                }
                                catch (FormatException)
                                {
                                    __command.Parameters.AddWithValue(parseName, QueryBuilder.DefaultValue(p.MemberType));
                                }
                                catch (OverflowException)
                                {
                                    __command.Parameters.AddWithValue(parseName, QueryBuilder.DefaultValue(p.MemberType));
                                }
                                vals.Add(parseName);
                                attrs.Add("[" + parseAttr + "]");


                                if (p.MemberType == MemberTypes.Ссылка)
                                {
                                    __command.Parameters.AddWithValue(parseName + "_НазваниеОбъекта",
                                        value.Значение.Equals(0)
                                            ? string.Empty
                                            : ПолучитьЗначение<string>(value.Значение, "НазваниеОбъекта", хранилище, domain)).SqlDbType = SqlDbType.VarChar;
                                    vals.Add(parseName + "_НазваниеОбъекта");
                                    attrs.Add("[" + parseAttr + ".НазваниеОбъекта]");
                                }
                            }

                            if (attrs.Count > 0)
                            {
                                __command.CommandText = string.Format(@"set nocount on; set ansi_warnings off; insert into {0} ([id_node],[type],{1}) values (@id_node,@type,{2});",
                                    item.Таблица,
                                    string.Join(",", attrs),
                                    string.Join(",", vals));
                                __command.CommandTimeout = 600;
                                __command.Parameters.AddWithValue("@id_node", id_node).SqlDbType = SqlDbType.Decimal;
                                __command.Parameters.AddWithValue("@type", ТипРаздела).SqlDbType = SqlDbType.VarChar;
                                __command.ExecuteNonQuery();
                            }
                        }
                        catch (Exception ex)
                        {
                            ConfigurationClient.WindowsLog("ДобавитьРаздел.ХешьТаблица", user, domain, ТипРаздела, __command.CommandText, ex.ToString(), db.Connection.ConnectionString);
                        }
                        finally
                        {
                            db.Connection.Close();
                        }
                        #endregion
                    }
                }//);
            }
            else
            {
                //Parallel.ForEach(СписокЗависимыхТаблиц, options, item =>
                foreach (var item in СписокЗависимыхТаблиц)
                {
                    using (var db = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX, item.Таблица.Substring(6)))
                    {
                        var vals = new List<string>(item.ЗависимыеАтрибуты.Length + 10);
                        var __command = (db.Connection as SqlConnection).CreateCommand();

                        #region Обновить таблицу
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                if (db.Connection.State != ConnectionState.Open)
                                    db.Connection.Open();

                                var isUnique = new HashSet<string>();
                                foreach (var p in item.ЗависимыеАтрибуты)
                                {
                                    if (isUnique.Contains(p.Атрибут))
                                        continue;

                                    isUnique.Add(p.Атрибут);
                                    var value = null as Value;
                                    if (!значения.TryGetValue(p.Атрибут, out value))
                                        continue;

                                    #region set default value
                                    if (value == null)
                                        value = new Value();
                                    if (value.Значение == null)
                                    {
                                        value.Значение = QueryBuilder.DefaultValue(p.MemberType);
                                    }
                                    #endregion

                                    var parseName = "@" + QueryColumn.ParseName(p.Атрибут);
                                    var parseAttr = QueryColumn.ParseAttrInsert(p.Атрибут);
                                    try
                                    {
                                        switch (p.MemberType)
                                        {
                                            case MemberTypes.Int:
                                            case MemberTypes.Double:
                                                __command.Parameters.AddWithValue(parseName, (value.Значение is string) ? Convert.ToDecimal(value.Значение) : value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.Decimal;
                                                break;

                                            case MemberTypes.Ссылка:
                                                __command.Parameters.AddWithValue(parseName, QueryBuilder.ResolveIdNode(value.Значение, хранилище, domain, false)).SqlDbType = SqlDbType.Decimal;
                                                //__command.Parameters.AddWithValue(parseName,  (value.Значение is string) ? Convert.ToDecimal(value.Значение) : value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.Decimal;
                                                break;

                                            case MemberTypes.DateTime:
                                                __command.Parameters.AddWithValue(parseName, (value.Значение is string) ? Convert.ToDateTime(value.Значение) : value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.DateTime;
                                                break;

                                            case MemberTypes.Bool:
                                                __command.Parameters.AddWithValue(parseName, (value.Значение is string) ? Convert.ToBoolean(value.Значение) : value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.Bit;
                                                break;

                                            case MemberTypes.String:
                                                __command.Parameters.AddWithValue(parseName, Convert.ToString(value.Значение) ?? Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                                break;

                                            default:
                                                if (p.ПолнотекстовыйВывод)
                                                {
                                                    __command.Parameters.AddWithValue(parseName, value.Значение ?? Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                                }
                                                else
                                                {
                                                    if (value.Значение == null)
                                                        __command.Parameters.AddWithValue(parseName, Convert.DBNull).SqlDbType = SqlDbType.VarChar;
                                                    else if (value.Значение is string)
                                                        __command.Parameters.AddWithValue(parseName, ((string)value.Значение).Length > 512 ? ((string)value.Значение).Substring(0, 512) : (string)value.Значение).SqlDbType = SqlDbType.VarChar;
                                                    else
                                                        __command.Parameters.AddWithValue(parseName, value.Значение).SqlDbType = SqlDbType.VarChar;
                                                }
                                                break;
                                        }
                                    }
                                    catch (FormatException)
                                    {
                                        __command.Parameters.AddWithValue(parseName, QueryBuilder.DefaultValue(p.MemberType));
                                    }
                                    catch (OverflowException)
                                    {
                                        __command.Parameters.AddWithValue(parseName, QueryBuilder.DefaultValue(p.MemberType));
                                    }
                                    vals.Add("[" + parseAttr + "] = " + parseName);


                                    if (p.MemberType == MemberTypes.Ссылка)
                                    {
                                        __command.Parameters.AddWithValue(parseName + "_НазваниеОбъекта",
                                            value.Значение.Equals(0)
                                                ? string.Empty
                                                : ПолучитьЗначение<string>(value.Значение, "НазваниеОбъекта", хранилище, domain)).SqlDbType = SqlDbType.VarChar;
                                        vals.Add("[" + parseAttr + ".НазваниеОбъекта] = " + parseName + "_НазваниеОбъекта");
                                    }
                                }

                                if (vals.Count > 0)
                                {
                                    __command.CommandText = string.Format(@"set nocount on; set ansi_warnings off; update {0} set {1} where [id_node] = @id_node;", item.Таблица, string.Join(",", vals));
                                    __command.CommandTimeout = 600;
                                    __command.Parameters.AddWithValue("@id_node", id_node).SqlDbType = SqlDbType.Decimal;
                                    __command.ExecuteNonQuery();
                                }

                                break;
                            }
                            catch (SqlException ex)
                            {
                                if (ex.Message.Contains("deadlocked"))
                                {
                                    System.Threading.Thread.Sleep(250);
                                    continue;
                                }
                                else
                                {
                                    ConfigurationClient.WindowsLog("СохранитьЗначение.ХешьТаблица / SqlException", user, domain, ТипРаздела, __command.CommandText, ex.ToString(), db.Connection.ConnectionString);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                ConfigurationClient.WindowsLog("СохранитьЗначение.ХешьТаблица", user, domain, ТипРаздела, __command.CommandText, ex.ToString(), db.Connection.ConnectionString);
                                break;
                            }
                            finally
                            {
                                db.Connection.Close();
                            }
                        }
                        #endregion
                    }
                }//);
            }
        }
        #endregion

        #region Кеш
        public object ПолучитьКешЗначение(string key, string domain)
        {
            var value = MemoryCache.Get(domain + ":" + key);
            if (value != null && value.obj != null)
                return value.obj.Значение;
            return null;
        }
        public void СохранитьКешЗначение(string key, object value, int timeout, string domain)
        {
            MemoryCache.Set(domain + ":" + key, new ValueContanier()
            {
                obj = new Value(value),
                cache = timeout == 0 ? DateTime.MinValue : DateTime.Now.AddSeconds(timeout)
            });
        }
        public void ОбновитьЗначениеВКеше(string ИмяКеша, decimal[] id_nodes, Dictionary<string, object> values, string user, string domain)
        {
            if (id_nodes.Length == 0) return;
            if (string.IsNullOrWhiteSpace(ИмяКеша)) 
                return;
            if (values != null && values.Count == 0) 
                return;

            var __command = null as SqlCommand;
            using (var db = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX, ИмяКеша))
            {
                __command = (db.Connection as SqlConnection).CreateCommand();

                try
                {
                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    __command.CommandText = string.Format(@"set nocount on; update cache_{0} set {1} where {2}", ИмяКеша,
                        string.Join("\n,", values.Select(p => string.Format("  [{0}] = @{1}", p.Key, p.Key.Replace("/", "").Replace(".", "")))),
                        // если Id всего один то просылаем его через параметр 
                        id_nodes.Length == 1
                            ? "id_node = @IdNode"
                            : string.Format("id_node in ({0})", string.Join(",", id_nodes.ToArray())));

                    __command.CommandTimeout = 300;

                    if (id_nodes.Length == 1)
                        __command.Parameters.AddWithValue("@IdNode", id_nodes[0]).SqlDbType = SqlDbType.Decimal;

                    foreach (var v in values)
                    {
                        var _value = v.Value;
                        if (v.Value is DateTime && DateTime.MinValue.Equals(v.Value))
                            _value = null;

                        if (v.Value is string && ((string)v.Value).Length > 512)
                            _value = ((string)v.Value).Substring(0, 512);

                        //конвертировать дату
                        if (v.Value is string && v.Key.StartsWith("Дата", StringComparison.CurrentCultureIgnoreCase))
                        {
                            DateTime date;
                            if(DateTime.TryParse((string)v.Value, out date))
                                _value = date;
                        }

                        __command.Parameters.AddWithValue("@" + v.Key.Replace("/", "").Replace(".", ""), _value);
                    }

                    __command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog("ОбновитьЗначениеВКеше", user, domain, ИмяКеша,
                        __command != null ? __command.CommandText : string.Empty, ex.ToString());
                }
                finally
                {
                    db.Connection.Close();
                }
            }

            #region hidden
            //            // обновляем в кеше
            //            var q = new RosService.Data.Query();

            //            foreach (var v in values)
            //                q.ДобавитьПараметр("@" + v.Key.Replace("/", ""), v.Value);

            //            if (id_nodes.Length == 1)
            //                q.ДобавитьПараметр("@IdNode", id_nodes[0]);

            //            q.Sql = string.Format(@"
            //update cache_{0}
            //set 
            //    {1}
            //where 
            //    {2}
            //                ",
            //                 ИмяКеша,
            //                 string.Join("\n,", values.Select(p => string.Format("  [{0}] = @{1}", p.Key, p.Key.Replace("/", "")))),
            //                // если Id всего один то просылаем его через параметр 
            //                id_nodes.Length == 1
            //                    ? "id_node = @IdNode"
            //                    : string.Format("id_node in ({0})", string.Join(",", id_nodes.ToArray())));

            //            Поиск(q, Хранилище.Оперативное, domain);
            #endregion
        }
        #endregion

        #region Асинхронно
        public decimal ДобавитьРазделАсинхронно(object id_parent, string тип, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            return ДобавитьРаздел(id_parent, тип, значения, ДобавитьВИсторию, хранилище, true, user, domain);
        }

        public void УдалитьРазделАсинхронно(bool ВКорзину, bool УдалитьЗависимыеОбъекты, decimal[] id_node, Хранилище хранилище, string user, string domain)
        {
            try { УдалитьРаздел(ВКорзину, УдалитьЗависимыеОбъекты, id_node, хранилище, user, domain); }
            catch (Exception ex) { ConfigurationClient.WindowsLog("УдалитьРазделАсинхронно", user, domain, ex.ToString()); }
        }
        public void УдалитьРазделПоискАсинхронно(bool ВКорзину, bool УдалитьЗависимыеОбъекты, Query запрос, Хранилище хранилище, string user, string domain)
        {
            try { УдалитьРазделПоиск(ВКорзину, УдалитьЗависимыеОбъекты, запрос, хранилище, user, domain); }
            catch (Exception ex) { ConfigurationClient.WindowsLog("УдалитьРазделПоискАсинхронно", user, domain, ex.ToString()); }
        }
        public void УдалитьПодразделыАсинхронно(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain)
        {
            try { УдалитьПодразделы(ВКорзину, id_node, хранилище, user, domain); }
            catch (Exception ex) { ConfigurationClient.WindowsLog("УдалитьПодразделыАсинхронно", user, domain, ex.ToString()); }
        }

        public void СохранитьЗначениеПоискАсинхронно(Query запрос, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            try { СохранитьЗначениеПоиск(запрос, значения, ДобавитьВИсторию, хранилище, user, domain); }
            catch (Exception ex) { ConfigurationClient.WindowsLog("СохранитьЗначениеПоискАсинхронно", user, domain, ex.ToString()); }
        }


        public void СохранитьЗначениеАсинхронно(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain)
        {
            СохранитьЗначениеАсинхронно(id_node, значения, ДобавитьВИсторию, false, хранилище, user, domain);
        }
        private void СохранитьЗначениеАсинхронно(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, bool IsNew, Хранилище хранилище, string user, string domain)
        {
            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
            if (значения == null || (0m).Equals(idParentNode))
                return;

            var __path = MemoryCache.Path(domain, хранилище, idParentNode);
            //var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
            //Parallel.ForEach(значения, options, item =>
            foreach (var item in значения)
            {
                #region Кешировать значение
                if (!item.Value.IsСписок)
                {
                    var contanier = new ValueContanier() { obj = new Value(item.Value.Значение), cache = DateTime.Now.Add(MemoryCache.Timeout) };
                    MemoryCache.Set(__path + item.Key, contanier);
                }
                #endregion
            }//);

            try
            {
                //СохранитьЗначение(idParentNode, значения, ДобавитьВИсторию, false, хранилище, user, domain, false);
                MemoryCache.AddTransaction(new MemoryTransaction()
                {
                    id_node = idParentNode,
                    values = значения,
                    IsNew = IsNew,
                    stage = хранилище,
                    user = user,
                    domain = domain
                });
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("СохранитьЗначениеАсинхронно", user, domain, ex.ToString());
            }
        }
        #endregion

        #region Разделы
        public void УдалитьРазделБезПодструктуры(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain)
        {
            if (ОтключитьУдаление)
                return;

            #region sql
            var prifixTable = GetTablePrefix(хранилище);
            var sb = new StringBuilder();
            sb.AppendLine("set nocount on\n");
            sb.AppendLine("------------------\n");
            sb.AppendFormat("delete from {0}tblNode where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueBool where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueByte where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueDate where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueDouble where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueHref where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueString where [id_node] = @id_node\n", prifixTable);

            foreach (var item in Cache.GetAll<КешХешьТаблица>(Cache.Key<КешХешьТаблица>(domain, ""))
                .Where(p => p.Value.Хранилище == хранилище))
            {
                sb.AppendFormat("delete from {0} where [id_node] = @id_node\n", ((КешХешьТаблица)item.Value).Таблица);
            }
            #endregion

            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    foreach (var item in id_node)
                    {
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = sb.ToString();
                        command.CommandTimeout = 300;
                        command.Parameters.AddWithValue("@id_node", item).SqlDbType = SqlDbType.Decimal;
                        command.ExecuteNonQuery();

                        MemoryCache.RemoveAll(MemoryCache.Path(domain, хранилище, item));
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        public void УдалитьРаздел(bool ВКорзину, bool УдалитьЗависимыеОбъекты, decimal[] id_node, Хранилище хранилище, string user, string domain)
        {
            if (!Cache.НеУдалятьСвязанныеОбъекты && УдалитьЗависимыеОбъекты)
            {
                var nodes = new System.Collections.Concurrent.ConcurrentBag<decimal>();
                if (id_node != null)
                {
                    //var option = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                    //Parallel.ForEach(id_node, option, item =>
                    foreach (var item in id_node)
                    {
                        foreach (var r in СписокЗависимыхРазделов(item, хранилище, domain).Select(p => p.id_node).ToArray())
                            nodes.Add(r);
                    }//);
                }
                Удалить(false, ВКорзину, nodes.Union(id_node), хранилище, user, domain);
            }
            else
            {
                Удалить(false, ВКорзину, id_node, хранилище, user, domain);
            }
        }
        public void УдалитьРазделПоиск(bool ВКорзину, bool УдалитьЗависимыеОбъекты, Query запрос, Хранилище хранилище, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            УдалитьРаздел(ВКорзину, УдалитьЗависимыеОбъекты,
                Поиск(запрос, хранилище, domain).Значение.AsEnumerable().Select(p => p.Field<decimal>("id_node")).ToArray(),
                хранилище, user, domain);
        }
        public void УдалитьПодразделы(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain)
        {
            Удалить(true, ВКорзину, id_node, хранилище, user, domain);
        }

        public static object lockDeleteObject = new System.Object();
        private static string _FileDelete;
        public string FileDelete
        {
            get
            {
                if (string.IsNullOrEmpty(_FileDelete))
                {
                    var asm = System.Reflection.Assembly.GetEntryAssembly();
                    if (asm != null)
                    {
                        _FileDelete = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(asm.Location), "log_delete.txt");
                    }
                }
                return _FileDelete;
            }
        }
        internal void Удалить(bool IsТолькоПодразделы, bool ВКорзину, IEnumerable<decimal> id_node, Хранилище хранилище, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            #region логирование
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    lock (lockDeleteObject)
                    {
                        if (string.IsNullOrEmpty(FileDelete))
                            return;

                        var sbl = null as StringBuilder;
                        if (!System.IO.File.Exists(FileDelete))
                        {
                            sbl = new StringBuilder();
                            sbl.AppendFormat("{0,-14}{1,-20}{2,-20}{3,-22}{4}", "Домен", "Пользователь", "Дата", "Тип", "Описание");
                            sbl.AppendLine();
                            sbl.AppendFormat("---------------------------------------------------------------------------------------");
                            sbl.AppendLine();
                            System.IO.File.WriteAllText(FileDelete, sbl.ToString());
                        }

                        sbl = new StringBuilder();
                        foreach (var item in id_node.Take(10))
                        {
                            var type = ПолучитьЗначение<string>(item, "Тип.Имя", хранилище, domain);
                            sbl.AppendLine(string.Format("{0,-14}{1,-20}{2,-20}{3,-22}{4:f0}:{5}", domain, user, DateTime.Now,
                                type.Length > 20 ? type.Substring(0, 20) : type,
                                item,
                                ПолучитьЗначение<string>(item, "НазваниеОбъекта", хранилище, domain)));
                        }
                        System.IO.File.AppendAllText(FileDelete, sbl.ToString());
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                }
            });
            #endregion

            if (ОтключитьУдаление)
                return;

            //Interlocked.Increment(ref Helper.Statistics.Delete);

            #region nodes
            var nodes = new List<decimal>(5000);
            var prifixTable = GetTablePrefix(хранилище);
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    foreach (var item in id_node)
                    {
                        var HashCode = ПолучитьЗначениеПростое(item, "HashCode", хранилище, domain);
                        if (HashCode == null || string.IsNullOrEmpty(Convert.ToString(HashCode)))
                            continue;

                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = string.Format(@"select id_node from {0}tblNode where [HashCode] like @HashCode + @prefix", prifixTable); ;
                        command.Parameters.AddWithValue("@HashCode", HashCode).SqlDbType = SqlDbType.VarChar;
                        command.Parameters.AddWithValue("@prefix", IsТолькоПодразделы ? "_%" : "%").SqlDbType = SqlDbType.VarChar;
                        var reader = command.ExecuteReader();
                        try
                        {
                            while (reader.Read())
                                nodes.Add((decimal)reader[0]);
                        }
                        finally
                        {
                            if (reader != null)
                                reader.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                }
                finally
                {
                    db.Connection.Close();
                }
            }
            #endregion

            #region sql
            var sb = new StringBuilder();
            //var sbCache = new StringBuilder();
            sb.AppendLine("set nocount on\n");
            sb.AppendFormat("delete from {0}tblNode where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueBool where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueByte where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueDate where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueDouble where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueHref where [id_node] = @id_node\n", prifixTable);
            sb.AppendFormat("delete from {0}tblValueString where [id_node] = @id_node\n", prifixTable);

            foreach (var item in Cache.GetAll<КешХешьТаблица>(Cache.Key<КешХешьТаблица>(domain, ""))
                .Where(p => p.Value.Хранилище == хранилище))
            {
                //sbCache.AppendFormat("delete from {0} where [id_node] = @id_node\n", ((КешХешьТаблица)item.Value).Таблица);
                using (RosService.DataClasses.ClientDataContext dbCache = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX, item.Value.Таблица.Substring(6)))
                {
                    try
                    {
                        var sql = string.Format("delete from {0} where [id_node] = @id_node\n", ((КешХешьТаблица)item.Value).Таблица);

                        if (dbCache.Connection.State != ConnectionState.Open)
                            dbCache.Connection.Open();

                        foreach (var i in nodes)
                        {
                            var command = (dbCache.Connection as SqlConnection).CreateCommand();
                            command.CommandText = sql;
                            command.CommandTimeout = 600;
                            command.Parameters.AddWithValue("@id_node", i).SqlDbType = SqlDbType.Decimal;
                            command.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                    }
                    finally
                    {
                        dbCache.Connection.Close();
                    }
                }
            }
            #endregion


            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            //using (RosService.DataClasses.ClientDataContext dbCache = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    //if (dbCache.Connection.State != ConnectionState.Open)
                    //    dbCache.Connection.Open();

                    foreach (var i in nodes)
                    {

                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = sb.ToString();
                        command.CommandTimeout = 600;
                        command.Parameters.AddWithValue("@id_node", i).SqlDbType = SqlDbType.Decimal;
                        command.ExecuteNonQuery();

                        //if (sbCache.Length > 0)
                        //{
                        //    command = (dbCache.Connection as SqlConnection).CreateCommand();
                        //    command.CommandText = sbCache.ToString();
                        //    command.CommandTimeout = (int)TimeSpan.FromMinutes(5).TotalSeconds;
                        //    command.Parameters.AddWithValue("@id_node", i).SqlDbType = SqlDbType.Decimal;
                        //    command.ExecuteNonQuery();
                        //}

                        //удалить значения в кеше
                        if (!MemoryCache.IsMemoryCacheClient)
                            MemoryCache.RemoveAll(MemoryCache.Path(domain, хранилище, i));
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                }
                finally
                {
                    db.Connection.Close();
                    //dbCache.Connection.Close();
                }
            }

            #region Удалить идентификаторы
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var nodesDic = nodes.ToDictionary(p => p);
                    foreach (var item in Cache.GetAllResolve(domain))
                    {
                        if (nodesDic.ContainsKey(item.Value.id_node))
                            Cache.RemoveResolve(item.Key);
                    }
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), user, domain);
                }
            });
            #endregion
        }
        public void ВосстановитьРаздел(object[] id_node, Хранилище хранилище, string user, string domain)
        {
            if (ОтключитьУдаление)
                return;

            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                    foreach (var item in id_node.Select(p => RosService.QueryBuilder.ResolveIdNode(p, хранилище, domain, false)))
                    {
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = string.Format(@"
                        set nocount on
                        ---------------
                        declare @HashCode varchar(900);
                        select @HashCode = [HashCode] + '%' from {0}tblNode WITH(NOLOCK) where id_node = @id_node;
                        if(@HashCode <> '')
                        begin
                            update {0}tblNode set [hide] = 0 where [HashCode] like @HashCode
                            update {0}tblValueBool set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            update {0}tblValueByte set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            update {0}tblValueDate set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            update {0}tblValueDouble set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            update {0}tblValueHref set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            update {0}tblValueString set [hide] = 0 where [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)

                            delete from {0}tblValueBool where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            delete from {0}tblValueByte where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            delete from {0}tblValueDate where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            delete from {0}tblValueDouble where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            delete from {0}tblValueHref where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                            delete from {0}tblValueString where [type] in ('УдалилРаздел','ДатаУдаленияРаздела') and [id_node] in (select [id_node] from {0}tblNode WITH(NOLOCK) where [HashCode] like @HashCode)
                        end",
                        GetTablePrefix(хранилище));
                        command.CommandTimeout = 300;
                        command.Parameters.AddWithValue("@id_node", item).SqlDbType = SqlDbType.Decimal;
                        command.Parameters.AddWithValue("@user", user).SqlDbType = SqlDbType.VarChar;
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }


        public void ИзменитьТипРаздела(object id_node, string НовыйТип, Хранилище хранилище, string domain)
        {
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
                    if (idParentNode == 0m)
                        return;

                    if (db.Connection.State != ConnectionState.Open)
                        db.Connection.Open();

                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = string.Format(@"update {0}tblNode set [type] = @type where [id_node] = @id_node", GetTablePrefix(хранилище));
                    command.Parameters.AddWithValue("@id_node", idParentNode).SqlDbType = SqlDbType.Decimal;
                    command.Parameters.AddWithValue("@type", НовыйТип).SqlDbType = SqlDbType.VarChar;
                    command.ExecuteNonQuery();

                    var __path = MemoryCache.Path(domain, хранилище, idParentNode);
                    var __keyfull = __path + "Тип.Имя";
                    var _tmp = MemoryCache.Get(__keyfull);
                    if (_tmp != null)
                    {
                        _tmp.obj.Значение = НовыйТип;
                        if (MemoryCache.IsMemoryCacheClient)
                            MemoryCache.Set(__keyfull, _tmp);
                    }

                    __keyfull = __path + "Тип.Описание";
                    MemoryCache.Remove(__keyfull);
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        public string ПолучитьТипРаздела(decimal id_node, Хранилище хранилище, string domain)
        {
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                try
                {
                    if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandTimeout = 600;
                    command.CommandText = @"set nocount on; select [type] from " + DataClient.GetTablePrefix(хранилище) + "tblNode WITH(NOLOCK) where [id_node] = @id_node";
                    command.Parameters.AddWithValue("@id_node", id_node).SqlDbType = SqlDbType.Decimal;
                    return Convert.ToString(command.ExecuteScalar());
                }
                finally
                {
                    db.Connection.Close();
                }
            }
        }
        #endregion

        #region Файлы
        private MimeType getMimeType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return MimeType.НеОпределен;
            else if (value.StartsWith("image/"))
                return MimeType.Изображение;
            else if (value.StartsWith("text/"))
                return MimeType.Text;
            else if (value == "application/msword")
                return MimeType.Word;
            else
                return MimeType.НеОпределен;
        }
        public IEnumerable<ФайлИнформация> СписокФайлов(decimal id_node, Хранилище хранилище, string domain)
        {
            return _СписокФайлов(id_node, хранилище, domain);
        }
        internal IEnumerable<ФайлИнформация> _СписокФайлов(object id_node, Хранилище хранилище, string domain)
        {
            var idParentNode = RosService.QueryBuilder.ResolveIdNode(id_node, хранилище, domain, false);
            if (idParentNode == 0) return new ФайлИнформация[0];

            var query = new Query();

            if (хранилище != Хранилище.Оперативное)
                query.CacheName = "Файлы:" + хранилище.ToString();
            else
                query.CacheName = "Файлы";

            query.Типы.Add("Файл%");
            query.ДобавитьВыводимыеКолонки("НазваниеОбъекта", "ДатаСозданияОбъекта", "РазмерФайла", "ОписаниеФайла",
                "РедакторРаздела", "MimeType", "ИдентификаторОбъекта", "ПолноеИмяФайла");
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = idParentNode, МаксимальнаяГлубина = 1 });

            return (from p in Поиск(query, хранилище, domain).AsEnumerable()
                    select new ФайлИнформация()
                    {
                        Имя = p.Field<string>("НазваниеОбъекта"),
                        ДатаСоздания = Convert.IsDBNull(p["ДатаСозданияОбъекта"]) ? DateTime.MinValue : p.Field<DateTime>("ДатаСозданияОбъекта"),
                        MimeType = getMimeType(p.Field<string>("MimeType")),
                        id_node = p.Field<decimal>("id_node"),
                        Описание = p.Field<string>("ОписаниеФайла"),
                        Размер = Convert.ToDouble(p["РазмерФайла"]),
                        ИдентификаторФайла = p.Field<string>("ИдентификаторОбъекта"),
                        Создатель = p.Field<string>("РедакторРаздела"),
                        ПолноеИмяФайла = p.Field<string>("ПолноеИмяФайла")
                    }).OrderBy(p => p.ДатаСоздания);
        }
        public int КоличествоФайлов(decimal id_node, Хранилище хранилище, string domain)
        {
            try
            {
                return ПолучитьЗначение<int>(id_node, "@@КоличествоФайлов", хранилище, domain);
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Cache
        public string SaveCacheObjects()
        {
            try
            {
                var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "cache");
                Caching.Cache.Сохранить(dir, false, true);
                return "";
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, "system", "SaveCacheObjects");
                return ex.Message;
            }
        }

        public string UpdateCacheObject(Query q, string domain)
        {
            try
            {
                if (q.IsEmpty || string.IsNullOrEmpty(domain))
                    return "Empty query";

                //если запрос пришел строкой разобрать его в класс
                q.Parse(domain);

                if (!q.IsSql && q.IsCache && !Cache.ОтключитьКешированиеТаблиц && q.CacheName != "__FullTextSearch")
                {
                    var хранилище = Хранилище.Оперативное;
                    var _keyquerydata = Cache.Key<КешХешьТаблица>(domain, q.CacheName);

                    using (var db2 = new RosService.DataClasses.ClientDataContext(domain))
                    {
                        var qb = new QueryBuilder(db2, хранилище, domain);
                        qb.Parse(q, РежимРазбора.КешированиеДанныхСозданиеТаблицы);

                        var __cachedata = new КешХешьТаблица()
                        {
                            IsComplite = true,
                            _Процент = 100,
                            Таблица = "cache_" + QueryColumn.ParseName(q.CacheName),
                            Хранилище = хранилище,
                            Error = ""
                        };

                        #region запомнить зафисимые колонки
                        __cachedata.ВремяЖизни = q.CacheDuration == null || q.CacheDuration == TimeSpan.Zero ? null : (DateTime?)DateTime.Now.Add(q.CacheDuration);
                        __cachedata.ЗависимыеТипы = (qb.Типы ?? new string[0]).Select(p => p.Trim('\'')).ToArray();
                        __cachedata.ЗависимыеАтрибуты = qb.Колонки
                            .Where(p => p.Value != null
                                && !string.IsNullOrEmpty(p.Value.OriginalName)
                                && p.Value.Тип != null)
                            .Select(p => new ХешьАтрибут() { Атрибут = p.Value.OriginalName, MemberType = p.Value.Тип.MemberType, ПолнотекстовыйВывод = p.Value.ПолнотекстовыйВывод ?? false }).ToArray();
                        #endregion

                        //Cache.Remove(_keyquerydata);
                        Cache.Set(_keyquerydata, __cachedata);
                    }
                }

                return "";
            }
            catch(Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, domain, "UpdateCacheObject", Query.Serialize(q));
                return ex.Message;
            }
        }
        #endregion

        #region Поиск
        public TableValue Поиск(Query запрос, Хранилище хранилище, string domain)
        {
            //проверка лицензии
            //var result = RosService.Security.Security.ValidDbAsync(domain);
            //if (!string.IsNullOrEmpty(result))
            //    throw new Exception(result);

            try
            {
                //var timer = System.Diagnostics.Stopwatch.StartNew();
                if (запрос.IsEmpty || string.IsNullOrEmpty(domain))
                    return new TableValue();

                //если запрос пришел строкой разобрать его в класс
                запрос.Parse(domain);

                //Interlocked.Increment(ref Helper.Statistics.Search);

                using (var ds = new DataSet("ds") { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                {
                    var value = new TableValue();
                    var КоличествоВыводимыхДанных = запрос.КоличествоВыводимыхДанных > 0 && !запрос.ИгнорироватьСтраницы ? запрос.КоличествоВыводимыхДанных : int.MaxValue;
                    var КоличествоВыводимыхСтраниц = запрос.КоличествоВыводимыхСтраниц > 0 ? запрос.КоличествоВыводимыхСтраниц * КоличествоВыводимыхДанных : int.MaxValue;
                    var _hashquery = запрос.GetHash(хранилище);
                    var _keyquery = Cache.Key<КешЗапрос>(domain, _hashquery);
                    var _cache = Cache.Get<КешЗапрос>(_keyquery);
                    var _connectorPrefix = null as string;

                    #region Файл
                    if (!string.IsNullOrEmpty(запрос.Файл))
                    {
                        if (запрос.IsDebug)
                            throw new DebugException(запрос.СтрокаЗапрос);

                        //timer.Stop();
                        //value.ВремяПодготовкиДанных = timer.ElapsedMilliseconds;
                        var file = null as byte[];
                        var idNode = null as RosService.Data.Query.Параметр;

                        if (запрос.МестаПоиска != null && запрос.МестаПоиска.Count > 0)
                        {
                            var query = new Query();
                            query.КоличествоВыводимыхДанных = 1;
                            query.КоличествоВыводимыхСтраниц = 1;
                            query.Типы.Add("Файл%");
                            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИдентификаторОбъекта", Значение = запрос.Файл });
                            query.ДобавитьВыводимыеКолонки(new string[] { "ИдентификаторОбъекта" });
                            query.МестаПоиска = запрос.МестаПоиска;
                            var fileNode = new DataClient().Поиск(query, хранилище, domain).AsEnumerable().SingleOrDefault();
                            if (fileNode != null)
                            {
                                file = new RosService.Files.FileClient().ПолучитьФайлПолностью(fileNode["id_node"], хранилище, domain);
                            }
                        }
                        else if ((idNode = запрос.Параметры.FirstOrDefault(p => p.Имя == "@id_node")) != null && idNode.Значение != null)
                        {
                            file = new RosService.Files.FileClient().ПолучитьФайлПолностьюПоНазванию(idNode.Значение, запрос.Файл, хранилище, domain);
                        }
                        else
                        {
                            file = new RosService.Files.FileClient().ПолучитьФайлПолностью(запрос.Файл, хранилище, domain);
                        }

                        if (file != null && file.Length > 0)
                        {
                            ds.ReadXml(new StringReader(System.Text.Encoding.Default.GetString(file)));
                            if (ds.Tables.Count > 0)
                            {
                                ds.Tables[0].TableName = "r";
                                value.SetTable(ds.Tables[0]);
                            }
                        }
                        return value;
                    }
                    #endregion

                    #region Атрибут
                    if (!string.IsNullOrEmpty(запрос.Атрибут))
                    {
                        if (запрос.IsDebug)
                            throw new DebugException(запрос.СтрокаЗапрос);


                        var file = null as byte[];
                        var idNode = null as RosService.Data.Query.Параметр;
                        if ((idNode = запрос.Параметры.FirstOrDefault(p => p.Имя == "@id_node")) != null && idNode.Значение != null)
                        {
                            var stream = string.Empty;
                            if (idNode.Значение != null && !0m.Equals(idNode.Значение))
                            {
                                stream = ПолучитьЗначение<string>(idNode.Значение, запрос.Атрибут, хранилище, domain);
                            }
                            if (!string.IsNullOrEmpty(stream))
                            {
                                ds.ReadXml(new StringReader(stream));
                                if (ds.Tables.Count > 0)
                                {
                                    ds.Tables[0].TableName = "r";
                                    value.SetTable(ds.Tables[0]);
                                }
                            }
                        }
                        return value;
                    }
                    #endregion

                    #region HTTP
                    if (!string.IsNullOrEmpty(запрос.СтрокаЗапрос)
                        && запрос.СтрокаЗапрос.First() != '['
                        && (запрос.СтрокаЗапрос.StartsWith("http://") || запрос.СтрокаЗапрос.StartsWith("https://")))
                    {
                        using (var web = new System.Net.WebClient())
                        {
                            web.Encoding = System.Text.Encoding.UTF8;
                            var xml = web.DownloadString(запрос.СтрокаЗапрос);
                            ds.ReadXml(new StringReader(xml));
                            if (ds.Tables.Count > 0)
                            {
                                ds.Tables[0].TableName = "r";
                                value.SetTable(ds.Tables[0]);
                            }
                        }
                        return value;
                    }
                    #endregion

                    #region Автоматически закешировать
                    if (!запрос.IsCache
                        && запрос.CacheLocation == Query.OutputCacheLocation.Server
                        && _cache != null && _cache.ВсегдаКешировать)
                    {
                        запрос.CacheName = _keyquery;
                    }
                    #endregion

                    #region IsDebug
                    if (запрос.IsDebug)
                    {
                        using (var db = new RosService.DataClasses.ClientDataContext(domain))
                        {
                            var command = null as SqlCommand;
                            if (!запрос.IsCache)
                                command = new QueryBuilder(db, хранилище, domain).Parse(запрос, РежимРазбора.ПоУмолчанию);
                            else
                                command = new QueryBuilder(db, хранилище, domain).ParseTable(запрос);


                            command.Parameters.AddWithValue("@PageSize", КоличествоВыводимыхДанных).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@CountSize", КоличествоВыводимыхСтраниц).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@CurrentPage", запрос.Страница).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@hide", запрос.ВКорзине).SqlDbType = SqlDbType.Bit;

                            var sb = new StringBuilder();
                            sb.AppendFormat("--{0}", _hashquery);
                            sb.AppendLine();
                            foreach (SqlParameter item in command.Parameters)
                            {
                                if (item.Value is decimal || item.Value is double || item.Value is int)
                                    sb.AppendFormat("declare {0} [numeric](18, 0) = {1:f0}\n", item.ParameterName, item.Value);
                                else if (item.Value is bool)
                                    sb.AppendFormat("declare {0} bit = {1}\n", item.ParameterName, new System.Data.SqlTypes.SqlBoolean(Convert.ToBoolean(item.Value)).ToSqlByte());
                                else if (item.Value is DateTime)
                                    sb.AppendFormat("declare {0} datetime = '{1:yyyy-MM-dd}'\n", item.ParameterName, item.Value);
                                else
                                    sb.AppendFormat("declare {0} varchar(255) = '{1}'\n", item.ParameterName, item.Value);
                            }
                            sb.AppendLine();
                            sb.AppendLine("---------------------------------");
                            sb.Append(command.CommandText);
                            throw new DebugException(sb.ToString());
                        }
                    }
                    #endregion

                    #region IsCache
                    var __IsCache = false;
                    var __cachedata = null as КешХешьТаблица;
                    if (!запрос.IsSql && запрос.IsCache && !Cache.ОтключитьКешированиеТаблиц && запрос.CacheName != "__FullTextSearch")
                    {
                        if (хранилище != Хранилище.Оперативное)
                        {
                            запрос.CacheName += ":" + хранилище.ToString();
                        }

                        switch (запрос.CacheLocation)
                        {
                            #region Server
                            case Query.OutputCacheLocation.Server:
                                {
                                    if (запрос.CacheReadOnly)
                                    {
                                        __IsCache = true;
                                    }
                                    else
                                    {
                                        var _keyquerydata = Cache.Key<КешХешьТаблица>(domain, запрос.CacheName);
                                        __cachedata = Cache.Get<КешХешьТаблица>(_keyquerydata);
                                        if (__cachedata == null)
                                        {
                                            ConfigurationClient.WindowsLog("Создание кеша " + _keyquerydata, "", domain, System.Diagnostics.EventLogEntryType.Information);

                                            __cachedata = new КешХешьТаблица() { Таблица = "cache_" + QueryColumn.ParseName(запрос.CacheName), Хранилище = хранилище, Error = "" };
                                            Cache.Set(_keyquerydata, __cachedata);
                                        }

                                        #region сбросить кешь если завершилось время жизни
                                        if (__cachedata.IsComplite)
                                        {
                                            if (__cachedata.ВремяЖизни != null && __cachedata.ВремяЖизни < DateTime.Now)
                                            {
                                                ConfigurationClient.WindowsLog("Удаление кеша " + _keyquerydata, "", domain, System.Diagnostics.EventLogEntryType.Information);

                                                __cachedata.ВремяЖизни = null;
                                                __cachedata.IsComplite = false;

                                                if (MemoryCache.IsMemoryCacheClient)
                                                    Cache.Set(_keyquerydata, __cachedata);
                                            }
                                            else
                                            {
                                                __IsCache = true;
                                                _keyquery = Cache.Key<КешЗапросХешьТаблица>(domain, _hashquery);
                                                _cache = Cache.Get<КешЗапрос>(_keyquery);
                                                _connectorPrefix = CONNECTOR_CACHE_PREFIX;
                                            }
                                        }
                                        else if (запрос.CacheReadOnly)
                                        {
                                            throw new DebugException(string.Format("Запрос не может быть выполнен, нет кэш таблицы {0}", запрос.CacheName));
                                        }
                                        #endregion

                                        #region создать кешь
                                        if (!__cachedata.IsComplite && !запрос.CacheReadOnly)
                                        {
                                            System.Threading.Tasks.Task.Factory.StartNew((e) =>
                                            {
                                                try
                                                {
                                                    lock (КешХешьТаблица.lockObject)
                                                    {
                                                        __cachedata = Cache.Get<КешХешьТаблица>(_keyquerydata);
                                                        if (__cachedata.IsComplite)
                                                            return;

                                                        if (!__cachedata.IsComplite && string.IsNullOrEmpty(__cachedata.Error))
                                                        {
                                                            //проверка на агрегацию
                                                            if (((Query)e).IsAggregate())
                                                                return;

                                                            #region создать процесс
                                                            if (__cachedata.id_proc == 0)
                                                            {
                                                                ConfigurationClient.WindowsLog("Ожидание построения кеша " + _keyquerydata, "", domain, System.Diagnostics.EventLogEntryType.Information);

                                                                __cachedata.id_proc = new ConfigurationClient().Процесс_СоздатьПроцесс("", string.Format("Ожидание кеша: {0}", __cachedata.Таблица), null, "", domain);
                                                                СохранитьЗначениеПростое(__cachedata.id_proc, "СтатусПроцесса", "Ожидает", false, Хранилище.Оперативное, "", domain);
                                                                Thread.Sleep(100);

                                                                if (MemoryCache.IsMemoryCacheClient)
                                                                    Cache.Set(_keyquerydata, __cachedata);
                                                            }
                                                            #endregion

                                                            var command = null as SqlCommand;
                                                            try
                                                            {
                                                                #region Статусы процесса
                                                                if (ПолучитьЗначение<string>(__cachedata.id_proc, "СтатусПроцесса", Хранилище.Оперативное, domain) == "Отменен")
                                                                {
                                                                    __cachedata.Error = "Процесс отменен";
                                                                    __cachedata._Процент = 100;

                                                                    if (MemoryCache.IsMemoryCacheClient)
                                                                        Cache.Set(_keyquerydata, __cachedata);

                                                                    return;
                                                                }
                                                                else
                                                                {
                                                                    var v = new Dictionary<string, Value>();
                                                                    v.Add("СтатусПроцесса", new Value("В работе"));
                                                                    v.Add("ДатаСозданияОбъекта", new Value(DateTime.Now));
                                                                    СохранитьЗначение(__cachedata.id_proc, v, false, Хранилище.Оперативное, "", domain);
                                                                    __cachedata._Процент = 0;
                                                                    Thread.Sleep(100);
                                                                }
                                                                #endregion

                                                                using (var db = new RosService.DataClasses.ClientDataContext(domain, CONNECTOR_CACHE_PREFIX, запрос.CacheName))
                                                                using (var db2 = new RosService.DataClasses.ClientDataContext(domain))
                                                                //using(var dsHash = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                                                                {
                                                                    var ВремяПодготовки = System.Diagnostics.Stopwatch.StartNew();
                                                                    var qb = new QueryBuilder(db2, хранилище, domain);
                                                                    qb.Parse((Query)e, РежимРазбора.КешированиеДанныхСозданиеТаблицы);

                                                                    #region запомнить зафисимые колонки
                                                                    __cachedata.ЗависимыеТипы = (qb.Типы ?? new string[0]).Select(p => p.Trim('\'')).ToArray();
                                                                    __cachedata.ЗависимыеАтрибуты = qb.Колонки
                                                                        .Where(p => p.Value != null
                                                                            && !string.IsNullOrEmpty(p.Value.OriginalName)
                                                                            && p.Value.Тип != null)
                                                                        .Select(p => new ХешьАтрибут() { Атрибут = p.Value.OriginalName, MemberType = p.Value.Тип.MemberType, ПолнотекстовыйВывод = p.Value.ПолнотекстовыйВывод ?? false }).ToArray();
                                                                    #endregion

                                                                    СохранитьЗначениеПростое(__cachedata.id_proc, "Описание", string.Format("Создание кеша: {0}", __cachedata.Таблица), false, Хранилище.Оперативное, "", domain);

                                                                    #region создать таблицу
                                                                    var totalRows = 0;
                                                                    try
                                                                    {
                                                                        var tablePrefix = DataClient.GetTablePrefix(хранилище);
                                                                        command = ((SqlConnection)db2.Connection).CreateCommand();
                                                                        command.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                                                                        if (qb.Типы.Count() == 1)
                                                                        {
                                                                            command.CommandText = string.Format("select count(*) from {1}tblNode WITH(NOLOCK) where [type] = {0}", qb.Типы.ElementAt(0), tablePrefix);
                                                                        }
                                                                        else
                                                                        {
                                                                            command.CommandText = string.Format("select count(*) from {1}tblNode WITH(NOLOCK) where [type] in ({0})", string.Join(",", qb.Типы), tablePrefix);
                                                                        }

                                                                        db2.Connection.Open();
                                                                        totalRows = Convert.ToInt32(command.ExecuteScalar());
                                                                    }
                                                                    finally
                                                                    {
                                                                        db2.Connection.Close();
                                                                    }

                                                                    //command.Parameters.AddWithValue("@CountSize", int.MaxValue).SqlDbType = SqlDbType.Int;
                                                                    //command.Parameters.AddWithValue("@PageSize", 0).SqlDbType = SqlDbType.Int;
                                                                    //command.Parameters.AddWithValue("@CurrentPage", 0).SqlDbType = SqlDbType.Int;
                                                                    //command.Parameters.AddWithValue("@UpdatePagging", true).SqlDbType = SqlDbType.Bit;

                                                                    //using (var adapter = new SqlDataAdapter(command))
                                                                    //{
                                                                    //    adapter.AcceptChangesDuringFill = false;
                                                                    //    adapter.AcceptChangesDuringUpdate = false;
                                                                    //    adapter.Fill(dsHash);
                                                                    //}

                                                                    //создать таблицу
                                                                    QueryBuilder.CreateTable(db.Connection as SqlConnection, __cachedata.Таблица, qb.Колонки);
                                                                    #endregion

                                                                    СохранитьЗначениеПростое(__cachedata.id_proc, "Описание", string.Format("Заполнение кеша: {0}", __cachedata.Таблица), false, Хранилище.Оперативное, "", domain);

                                                                    #region Запонить данными
                                                                    if (totalRows > 0)
                                                                    {
                                                                        var count = 1000;
                                                                        var pages = Math.Ceiling(Convert.ToDouble(totalRows) / (double)count);

                                                                        //var time = System.Diagnostics.Stopwatch.StartNew();
                                                                        //var log = new StringBuilder();
                                                                        command = qb.Parse((Query)e, РежимРазбора.КешированиеДанныхЗаполнение);

                                                                        //log.AppendLine("Parse: " + time.ElapsedMilliseconds.ToString());
                                                                        //System.IO.File.WriteAllText(@"c:\log.txt", log.ToString());
                                                                        //System.IO.File.WriteAllText(@"c:\query.fill.txt", command.CommandText);
                                                                        //time.Restart();

                                                                        //var option = new ParallelOptions() { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) };
                                                                        //var option = new ParallelOptions() { MaxDegreeOfParallelism = 1 };

                                                                        //ограничить загрузку данных
                                                                        //if(Convert.ToDouble(dsHash.Tables[0].Rows[0]["TotalRows"]) > 1000000)
                                                                        //    pages = pages * 0.5;

                                                                        var oRow = 0;

                                                                        try
                                                                        {
                                                                            if (db.Connection.State != ConnectionState.Open)
                                                                                db.Connection.Open();

                                                                            var sqlBulkCopy = new SqlBulkCopy(db.Connection as SqlConnection);
                                                                            sqlBulkCopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                                                                            sqlBulkCopy.DestinationTableName = __cachedata.Таблица;

                                                                            var command1 = (db2.Connection as SqlConnection).CreateCommand();
                                                                            command1.CommandText = command.CommandText;
                                                                            command1.CommandType = CommandType.Text;
                                                                            command1.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                                                                            command1.Parameters.AddWithValue("@CountSize", int.MaxValue).SqlDbType = SqlDbType.Int;
                                                                            command1.Parameters.AddWithValue("@PageSize", count).SqlDbType = SqlDbType.Int;
                                                                            command1.Parameters.AddWithValue("@UpdatePagging", true).SqlDbType = SqlDbType.Bit;
                                                                            command1.Parameters.AddWithValue("@CurrentPage", 0).SqlDbType = SqlDbType.Int;

                                                                            for (long i = 0; i < (long)pages; i++)
                                                                            {
                                                                                if (i > 0)
                                                                                {
                                                                                    command1.Parameters["@UpdatePagging"].Value = false;
                                                                                }
                                                                                command1.Parameters["@CurrentPage"].Value = i;

                                                                                //log.AppendLine("Init: " + time.ElapsedMilliseconds.ToString());
                                                                                //System.IO.File.WriteAllText(@"c:\log.txt", log.ToString());
                                                                                //time.Restart();

                                                                                using (var dsCache = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                                                                                {
                                                                                    using (var adapter = new SqlDataAdapter(command1))
                                                                                    {
                                                                                        adapter.AcceptChangesDuringFill = false;
                                                                                        adapter.AcceptChangesDuringUpdate = false;
                                                                                        adapter.Fill(dsCache);
                                                                                    }

                                                                                    sqlBulkCopy.ColumnMappings.Clear();
                                                                                    if (dsCache.Tables.Count > 0)
                                                                                    {
                                                                                        var table = dsCache.Tables[0];
                                                                                        foreach (DataColumn column in table.Columns)
                                                                                        {
                                                                                            sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                                                        }
                                                                                        sqlBulkCopy.WriteToServer(table);
                                                                                    }
                                                                                    oRow++;

                                                                                    //log.AppendLine("Save: " + time.ElapsedMilliseconds.ToString());
                                                                                    //System.IO.File.WriteAllText(@"c:\log.txt", log.ToString());
                                                                                    //time.Restart();
                                                                                }


                                                                                __cachedata._Процент = (float)(oRow) / (float)pages * 100f;
                                                                                new ConfigurationClient().Процесс_ОбновитьСостояниеПроцесса(__cachedata.id_proc, __cachedata._Процент, domain);

                                                                                if (MemoryCache.IsMemoryCacheClient)
                                                                                    Cache.Set(_keyquerydata, __cachedata);

                                                                            }
                                                                        }
                                                                        finally
                                                                        {
                                                                            if (db.Connection != null)
                                                                                db.Connection.Close();
                                                                        }
                                                                    }
                                                                    #endregion

                                                                    СохранитьЗначениеПростое(__cachedata.id_proc, "Описание", string.Format("Создание индексов: {0}", __cachedata.Таблица), false, Хранилище.Оперативное, "", domain);

                                                                    #region Создать индексы
                                                                    if (!string.IsNullOrEmpty(qb.Индексы))
                                                                    {
                                                                        try
                                                                        {
                                                                            if (db.Connection.State != ConnectionState.Open)
                                                                                db.Connection.Open();

                                                                            command = (db.Connection as SqlConnection).CreateCommand();
                                                                            command.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                                                                            command.CommandText = qb.Индексы;
                                                                            command.ExecuteNonQuery();
                                                                        }
                                                                        finally
                                                                        {
                                                                            if (db.Connection != null)
                                                                                db.Connection.Close();
                                                                        }
                                                                    }
                                                                    #endregion

                                                                    ВремяПодготовки.Stop();
                                                                    __cachedata.IsComplite = true;
                                                                    __cachedata.ВремяЖизни = ((Query)e).CacheDuration == null || ((Query)e).CacheDuration == TimeSpan.Zero ? null : (DateTime?)DateTime.Now.Add(((Query)e).CacheDuration);
                                                                    __cachedata.ВремяПодготовки = ВремяПодготовки.Elapsed.ToString();
                                                                    //__cachedata.AvgTime = (uint)ВремяПодготовки.ElapsedMilliseconds;
                                                                    __cachedata.Error = "";
                                                                    __cachedata._Процент = 100;

                                                                    if (MemoryCache.IsMemoryCacheClient)
                                                                        Cache.Set(_keyquerydata, __cachedata);

                                                                    СохранитьЗначениеПростое(__cachedata.id_proc, "Описание", string.Format("Кеш создан: {0}", __cachedata.Таблица), false, Хранилище.Оперативное, "", domain);
                                                                    new ConfigurationClient().Процесс_ЗавершитьПроцесс(__cachedata.id_proc, domain);
                                                                }
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                __cachedata.Error = ex.Message;

                                                                if (MemoryCache.IsMemoryCacheClient)
                                                                    Cache.Set(_keyquerydata, __cachedata);

                                                                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, domain, "RosService.Поиск", command != null ? command.CommandText : "");
                                                                new ConfigurationClient().Процесс_ОшибкаВПроцессе(__cachedata.id_proc, ex.Message, domain);
                                                            }
                                                            finally
                                                            {
                                                                //#drop_temp_index

                                                                if (command != null && command.Connection != null)
                                                                    command.Connection.Close();
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, domain, "RosService.Поиск.НачалоПостроенияКеша");
                                                }
                                            },
                                            запрос);

                                            throw new DebugException(string.Format("Запрос не может быть выполнен, идет построение данных - {0:f2} готово. Повторите попытку позже.", __cachedata._Процент));
                                        }
                                        #endregion
                                    }
                                }
                                break;
                            #endregion

                            #region Memory
                            case Query.OutputCacheLocation.Memory:
                                {
                                    var _keymemory = Cache.Key<КешХешьТаблицаПамять>(domain, запрос.CacheName);
                                    var __dump = Cache.Get<КешХешьТаблицаПамять>(_keymemory);
                                    if (__dump != null
                                        && (__dump.ВремяЖизни == null || __dump.ВремяЖизни > DateTime.Now)
                                        && __dump.Результат != null)
                                    {
                                        //timer.Stop();
                                        //__dump.Результат.ВремяПодготовкиДанных = timer.ElapsedMilliseconds;
                                        return __dump.Результат;
                                    }
                                }
                                break;
                            #endregion
                        }
                    }
                    #endregion

                    #region Первый вызов
                    if (_cache == null)
                    {
                        using (var db = new RosService.DataClasses.ClientDataContext(domain, _connectorPrefix, запрос.CacheName))
                        {
                            var qb = new QueryBuilder(db, хранилище, domain);
                            var command = !__IsCache
                                ? qb.Parse(запрос, РежимРазбора.ПоУмолчанию)
                                : qb.ParseTable(запрос);

                            #region Кеширование запроса
                            _cache = !__IsCache ? new КешЗапрос() : new КешЗапросХешьТаблица();
                            _cache.Sql = command.CommandText;
                            _cache.IsПоискПоСодержимому = запрос.IsПоискПоСодержимому;
                            _cache.Параметры = qb.Параметры ?? new Dictionary<string, MemberTypes?>();
                            _cache.Типы = (qb.Типы ?? new string[0]).Select(p => p.Trim('\''));

                            if (!запрос.IsSql)
                            {
                                Cache.Set(_keyquery, _cache);
                            }
                            #endregion
                        }
                    }
                    #endregion

                    #region Выполнить из кеша запросов
                    if (_cache != null)
                    {
                        if (запрос.IsSql && запрос.CacheName != "__FullTextSearch")
                            _connectorPrefix = CONNECTOR_CACHE_PREFIX;

                        using (var db = new RosService.DataClasses.ClientDataContext(domain, _connectorPrefix, запрос.CacheName))
                        {
                            var command = new QueryBuilder(db, хранилище, domain).ParseFromCache(запрос, _cache);
                            command.CommandTimeout = 600;
                            command.Parameters.AddWithValue("@PageSize", КоличествоВыводимыхДанных).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@CountSize", КоличествоВыводимыхСтраниц).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@CurrentPage", запрос.Страница).SqlDbType = SqlDbType.Int;
                            command.Parameters.AddWithValue("@hide", запрос.ВКорзине).SqlDbType = SqlDbType.Bit;

                            #region Выполнить запрос
                            using (var adapter = new SqlDataAdapter(command))
                            {
                                adapter.AcceptChangesDuringFill = false;
                                adapter.AcceptChangesDuringUpdate = false;
                                adapter.Fill(ds);
                            }
                            value.Page = запрос.Страница;

                            if (ds.Tables.Count > 0)
                            {
                                ds.Tables[0].TableName = "r";
                                value.SetTable(ds.Tables[0]);
                            }

                            if (ds.Tables.Count > 1 && ds.Tables[1].Columns.Contains("TotalRows"))
                            {
                                value.Count = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRows"]);
                                value.PageCount = Convert.ToInt32(ds.Tables[1].Rows[0]["PageCount"]);
                            }
                            else
                            {
                                value.Count = ds.Tables.Count != 0 ? ds.Tables[0].Rows.Count : 0;
                                value.PageCount = 1;
                            }
                            #endregion

                            //_cache.AvgTime = (uint)timer.ElapsedMilliseconds;
                        }
                    }
                    #endregion

                    #region Сохранить результат в кеш память [Memory]
                    if (запрос.IsCache && !Cache.ОтключитьКешированиеТаблиц)
                    {
                        switch (запрос.CacheLocation)
                        {
                            case Query.OutputCacheLocation.Memory:
                                {
                                    #region Сохранить в памяти
                                    //освободить память
                                    var _keymemory = Cache.Key<КешХешьТаблицаПамять>(domain, запрос.CacheName);
                                    var __КешХешьТаблицаПамять = Cache.Get<КешХешьТаблицаПамять>(_keymemory);
                                    if (__КешХешьТаблицаПамять != null)
                                    {
                                        var dispose = __КешХешьТаблицаПамять.Результат;
                                        if (dispose != null && dispose.Значение is IDisposable)
                                        {
                                            ((IDisposable)dispose.Значение).Dispose();
                                            dispose.SetTable(null);
                                        }
                                    }

                                    Cache.Set(_keymemory, new КешХешьТаблицаПамять()
                                    {
                                        ВремяЖизни = запрос.CacheDuration == null || запрос.CacheDuration == TimeSpan.Zero ? null : (DateTime?)DateTime.Now.Add(запрос.CacheDuration),
                                        Результат = value
                                    });
                                    #endregion
                                }
                                break;
                        }
                    }
                    #endregion

                    //timer.Stop();
                    //value.ВремяПодготовкиДанных = timer.ElapsedMilliseconds;
                    return value;
                }
            }
            catch (DebugException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, domain, "Поиск", Query.Serialize(запрос));
                throw ex;
            }
        }


        public decimal ПоискРазделаПоИдентификаторуОбъекта(string ИдентификаторОбъекта, Хранилище хранилище, string domain)
        {
            return ПоискРазделаПоИдентификаторуОбъекта(ИдентификаторОбъекта, хранилище, domain, true);
        }
        public decimal ПоискРазделаПоИдентификаторуОбъекта(string ИдентификаторОбъекта, Хранилище хранилище, string domain, bool isTry)
        {
            if (string.IsNullOrEmpty(ИдентификаторОбъекта))
            {
                if (isTry)
                    throw new Exception("Идентификатор объекта не задан.");
                return 0;
            }

            var __cachePathNode = Cache.KeyResolve(domain, ИдентификаторОбъекта);
            var cacheItem = Cache.GetResolve(__cachePathNode);
            if (cacheItem != null)
            {
                return cacheItem.id_node;
            }
            else
            {
                var attr = "ИдентификаторОбъекта";
                var value = ИдентификаторОбъекта;
                var type = null as Configuration.Type;
                //&ЛогинПользователя=Техподдержка
                //client.Get("&ЛогинПользователя=Техподдержка", "НазваниеОбъекта");
                //client.Get("&Биржа=3242342", "НазваниеОбъекта");
                if (ИдентификаторОбъекта.StartsWith("&"))
                {
                    var token = ИдентификаторОбъекта.Split('=');
                    if (token.Length == 2)
                    {
                        attr = token.ElementAt(0).Substring(1);
                        value = token.ElementAt(1);
                        type = new ConfigurationClient().ПолучитьТип(attr, domain);
                    }
                }

                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                        var command = (db.Connection as SqlConnection).CreateCommand();

                        if (type == null || type.RegisterType == RegisterTypes.string_value)
                        {
                            command.CommandText = string.Format("select [id_node] from {0}tblValueString WITH(NOLOCK) where [type] = '{1}' and [string_value_index] = @value", GetTablePrefix(хранилище), attr);
                            command.Parameters.AddWithValue("@value", value).SqlDbType = SqlDbType.VarChar;
                        }
                        else if (type.RegisterType == RegisterTypes.double_value)
                        {
                            switch (type.MemberType)
                            {
                                case MemberTypes.Int:
                                case MemberTypes.Double:
                                    command.CommandText = string.Format("select [id_node] from {0}tblValueDouble WITH(NOLOCK) where [type] = '{1}' and [double_value] = @value", GetTablePrefix(хранилище), attr);
                                    command.Parameters.AddWithValue("@value", Convert.ToDecimal(value)).SqlDbType = SqlDbType.Decimal;
                                    break;

                                case MemberTypes.Ссылка:
                                    command.CommandText = string.Format("select [id_node] from {0}tblValueHref WITH(NOLOCK) where [type] = '{1}' and [double_value] = @value", GetTablePrefix(хранилище), attr);
                                    command.Parameters.AddWithValue("@value", QueryBuilder.ResolveIdNode(value, хранилище, domain, false)).SqlDbType = SqlDbType.Decimal;
                                    break;
                            }
                        }

                        cacheItem = new RosService.Caching.КешИдентификаторРаздела();
                        cacheItem.id_node = Convert.ToDecimal(command.ExecuteScalar());
                    }
                    finally
                    {
                        db.Connection.Close();
                    }

                    if (cacheItem.id_node == 0 && isTry)
                    {
                        throw new Exception(string.Format("Идентификатор объекта '{0}' не найден.", ИдентификаторОбъекта));
                    }
                    else if (cacheItem.id_node > 0)
                    {
                        Cache.SetResolve(__cachePathNode, cacheItem);
                    }

                    return cacheItem.id_node;
                }
            }
        }
        public decimal ПоискРазделаПоКлючу(string HashCode, Хранилище хранилище, string domain)
        {
            return ПоискРазделаПоКлючу(HashCode, хранилище, domain, true);
        }
        public decimal ПоискРазделаПоКлючу(string HashCode, Хранилище хранилище, string domain, bool isTry)
        {
            if (string.IsNullOrEmpty(HashCode))
            {
                if (isTry)
                    throw new Exception("Ключ раздела не задан.");
                return 0;
            }

            var __cachePathNode = Cache.KeyResolve(domain, "&" + HashCode);
            var cacheItem = Cache.GetResolve(__cachePathNode);
            if (cacheItem != null)
            {
                return cacheItem.id_node;
            }
            else
            {
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();

                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandText = string.Format("select [id_node] from {0}tblNode WITH(NOLOCK) where [GuidCode] = @guid", GetTablePrefix(хранилище));
                        command.Parameters.AddWithValue("@guid", HashCode).SqlDbType = SqlDbType.VarChar;

                        cacheItem = new RosService.Caching.КешИдентификаторРаздела();
                        cacheItem.id_node = Convert.ToDecimal(command.ExecuteScalar());
                    }
                    finally
                    {
                        db.Connection.Close();
                    }

                    if (cacheItem.id_node == 0 && isTry)
                        throw new Exception(string.Format("Ключ раздела '{0}' не найден.", HashCode));
                    else
                        Cache.SetResolve(__cachePathNode, cacheItem);

                    return cacheItem.id_node;
                }
            }
        }
        #endregion

        #region История
        public TableValue ПоискИстории(Query запрос, Хранилище хранилище, string domain)
        {
            //если запрос пришел строкой разобрать его в класс
            запрос.Parse(domain);


            var value = new TableValue();
            var ds = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false };
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                var command = (db.Connection as SqlConnection).CreateCommand();

                var Колонки = new Dictionary<string, QueryColumn>();
                new QueryBuilder(db) { Колонки = Колонки }.ResolveAttributes(запрос.ВыводимыеКолонки.Select(p => new Query.Колонка() { Атрибут = p.Атрибут }), domain);

                var sb = new StringBuilder();
                sb.AppendLine(@"set nocount on");
                sb.AppendLine(@"-----------------");
                sb.AppendLine(@";with");
                sb.AppendFormat("\tnodes as (select [user], [date] from tblHistoryValue WITH(NOLOCK) where [id_node] = @id_node and [storage] = @Хранилище and [type] in ({0}) group by [user], [date])",
                    string.Join(",", Колонки.Select(p => "'" + p.Value.Тип.Name + "'")));

                foreach (var item in Колонки.Values)
                {
                    if (item.Тип == null) continue;

                    if (item.Тип.RegisterType == RegisterTypes.undefined) continue;
                    else if (item.Тип.IsReadOnly) continue;
                    else
                    {
                        sb.AppendFormat(",\n\t{0} as (select [user], [date], [{2}] value from tblHistoryValue WITH(NOLOCK) where [id_node] = @id_node and [storage] = @Хранилище and [type] = '{1}')", item.Name, item.Тип.Name, item.Тип.RegisterType.ToString());
                        if (item.Тип.MemberType == MemberTypes.Ссылка)
                            sb.AppendFormat(",\n\t{1}_НазваниеОбъекта as (select id_node, [string_value] value from {0}tblValueString WITH(NOLOCK) where [type] = 'НазваниеОбъекта')", GetTablePrefix(Хранилище.Оперативное), item.Name);
                    }
                }


                #region select
                sb.AppendLine("select top 20");
                sb.AppendLine("\tnodes.[user],");
                sb.Append("\tnodes.[date]");

                foreach (var item in Колонки.Values)
                {
                    if (item.Тип == null || item.Тип.RegisterType == RegisterTypes.undefined) continue;
                    switch (item.Тип.MemberType)
                    {
                        case MemberTypes.Double:
                        case MemberTypes.Byte:
                            {
                                sb.AppendFormat(",\n\tisnull({0}.[value],0) '{1}'", item.Name, item.OriginalName);
                            }
                            break;

                        case MemberTypes.Ссылка:
                            {
                                sb.AppendFormat(",\n\tisnull({0}.[value],0) '{1}'", item.Name, item.OriginalName);
                                sb.AppendFormat(",\n\tisnull({0}_НазваниеОбъекта.[value],'') '{1}.НазваниеОбъекта'", item.Name, item.OriginalName);
                            }
                            break;

                        case MemberTypes.Int:
                            {
                                sb.AppendFormat(",\n\tisnull(convert(int, {0}.[value]),0) '{1}'", item.Name, item.OriginalName);
                            }
                            break;

                        case MemberTypes.Bool:
                            {
                                sb.AppendFormat(",\n\tisnull(convert(bit, {0}.[value]),0) '{1}'", item.Name, item.OriginalName);
                            }
                            break;

                        case MemberTypes.DateTime:
                            {
                                sb.AppendFormat(",\n\t{0}.[value] '{1}'", item.Name, item.OriginalName);
                            }
                            break;

                        default:
                            {
                                sb.AppendFormat(",\n\tisnull({0}.[value],'') '{1}'", item.Name, item.OriginalName);
                            }
                            break;
                    }
                }
                #endregion

                #region join
                sb.AppendLine("\nfrom");
                sb.AppendLine("\tnodes");

                foreach (var item in Колонки.Values)
                {
                    if (item == null || item.Тип == null || item.Тип.RegisterType == RegisterTypes.undefined)
                        continue;
                    else
                    {
                        sb.AppendFormat("\n\tleft join {0} on nodes.[user] = {0}.[user] and nodes.[date] = {0}.[date]", item.Name);
                        if (item.Тип.MemberType == MemberTypes.Ссылка)
                            sb.AppendFormat("\n\tleft join {0}_НазваниеОбъекта on {0}_НазваниеОбъекта.id_node = {0}.[value]", item.Name);
                    }
                }
                #endregion

                #region order by
                sb.AppendLine("\norder by");
                sb.AppendLine("\t[date] desc");
                #endregion

                if (запрос.IsDebug)
                {
                    throw new Exception(sb.ToString());
                }

                command.CommandText = sb.ToString();
                var param_node = запрос.Параметры.FirstOrDefault(p => p.Имя.Equals("@id_node"));
                if (param_node == null) throw new Exception("Для поиска в истории необходимо указать @id_node раздела.");
                command.Parameters.AddWithValue("@id_node", param_node.Значение).SqlDbType = SqlDbType.Decimal;
                command.Parameters.AddWithValue("@Хранилище", (int)хранилище).SqlDbType = SqlDbType.Int;

                using (var adapter = new SqlDataAdapter(command))
                {
                    adapter.AcceptChangesDuringFill = false;
                    adapter.AcceptChangesDuringUpdate = false;
                    adapter.Fill(ds);

                    ds.Tables[0].TableName = "tblValue";
                    value.SetTable(ds.Tables[0]);
                    value.Page = 0;
                    value.Count = Convert.ToInt32(ds.Tables[0].Rows.Count);
                    value.PageCount = 1;
                }
            }

            if (value.Значение == null)
                value.SetTable(new DataTable("tblValue"));
            return value;
        }
        public TableValue ПолучитьИсторию(decimal id_node, Хранилище хранилище, string domain)
        {
            var value = new TableValue();
            var ds = new DataSet();
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
            {
                var command = (db.Connection as SqlConnection).CreateCommand();
                command.CommandText = @"
                set nocount on
                ---------------
                ;with
	                nodes as (select * from [tblHistoryValue] WITH(NOLOCK) where [storage] = @Хранилище and [id_node] = @id_node),
	                a as (select Name, Описание, BaseType from [assembly_tblAttribute] WITH(NOLOCK) where id_parent = 0)
                select top(100)
	                isnull(a.Name,'')     'Атрибут',
	                isnull(a.Описание,'') 'Атрибут.Описание',
	                isnull(a.BaseType,'') 'Тип',
	                nodes.[date],
	                nodes.[type],
	                nodes.[double_value],
	                nodes.[datetime_value],
	                nodes.[string_value],
	                isnull(nodes.[user],'') 'user'
                from	
	                nodes
	                left join a on a.[Name] = nodes.[type]";
                command.Parameters.AddWithValue("@id_node", id_node).SqlDbType = SqlDbType.Decimal;
                command.Parameters.AddWithValue("@Хранилище", (int)хранилище).SqlDbType = SqlDbType.Int;
                new SqlDataAdapter(command).Fill(ds);

                ds.Tables[0].TableName = "tblValue";
                value.SetTable(ds.Tables[0]);
                value.Page = 0;
                value.Count = Convert.ToInt32(ds.Tables[0].Rows.Count);
                value.PageCount = 1;
            }
            return value;
        }
        #endregion


        internal static string TableToXml(DataTable table)
        {
            var sb = new StringBuilder();
            table.WriteXml(new StringWriter(sb), XmlWriteMode.WriteSchema);
            return sb.ToString();
        }
        #endregion

        #region Сервисные
        public decimal КопироватьРаздел(URI КопироватьИз, URI КопироватьВ, string user, string domain)
        {
            КопироватьИз.node = RosService.QueryBuilder.ResolveIdNode(КопироватьИз.node, КопироватьИз.хранилище, КопироватьИз.domain, false);
            КопироватьВ.node = RosService.QueryBuilder.ResolveIdNode(КопироватьВ.node, КопироватьВ.хранилище, КопироватьВ.domain, false);
            if (КопироватьИз.node.Equals(0m) || КопироватьВ.node.Equals(0m))
                return 0;

            //var id_node_new = 0M;
            var copy = null as Func<decimal, decimal, decimal>;
            copy = delegate(decimal _id_parent, decimal _id_node)
            {
                var ТипДанных = ПолучитьЗначение<string>(_id_node, "Тип.Имя", КопироватьИз.хранилище, КопироватьИз.domain); //ПолучитьТипРаздела(_id_node, КопироватьИз.хранилище, КопироватьИз.domain);
                var values = new Dictionary<string, Value>();
                foreach (var item in new ConfigurationClient().СписокАтрибутов(ТипДанных, КопироватьИз.domain))
                {
                    if (item.IsReadOnly) continue;
                    if (КопироватьИз.domain == КопироватьВ.domain && item.IsAutoIncrement) continue;
                    //if (item.MemberType == MemberTypes.Таблица) continue;
                    if (item.Name == "ДатаСозданияОбъекта"
                        || item.Name == "РедакторРаздела"
                        || item.Name == "@РодительскийРаздел") continue;

                    var _value = ПолучитьЗначение<object>(_id_node, item.Name, КопироватьИз.хранилище, КопироватьИз.domain);
                    if (_value is DataTable)
                    {
                        var tmp = new Value() { IsСписок = true };
                        tmp.SetTable((DataTable)_value);
                        values.Add(item.Name, tmp);
                    }
                    else
                        values.Add(item.Name, new Value(_value));
                }

                var id_node = ДобавитьРаздел(_id_parent, ТипДанных, values, false, КопироватьВ.хранилище, user, КопироватьВ.domain);
                var query = new RosService.Data.Query();
                query.ДобавитьМестоПоиска(_id_node, 1);
                //foreach (var item in СписокРазделов(_id_node, null, null, 0, КопироватьИз.хранилище, КопироватьИз.domain))
                foreach (var item in Поиск(query, КопироватьИз.хранилище, КопироватьИз.domain).AsEnumerable())
                {
                    copy(id_node, item.Field<decimal>("id_node"));
                    //copy(id_node, item.id_node);
                }
                return id_node;
            };
            return copy(Convert.ToDecimal(КопироватьВ.node), Convert.ToDecimal(КопироватьИз.node));
        }

        //public decimal КопироватьРаздел(URI КопироватьИз, URI КопироватьВ, string user, string domain)
        //{
        //    КопироватьИз.node = RosService.QueryBuilder.ResolveIdNode(КопироватьИз.node, КопироватьИз.хранилище, КопироватьИз.domain, false);
        //    КопироватьВ.node = RosService.QueryBuilder.ResolveIdNode(КопироватьВ.node, КопироватьВ.хранилище, КопироватьВ.domain, false);
        //    if (КопироватьИз.node.Equals(0m) || КопироватьВ.node.Equals(0m))
        //        return 0;

        //    var copy = null as Func<decimal, decimal, decimal>;
        //    copy = delegate(decimal _id_parent, decimal _id_node)
        //    {
        //        var ТипДанных = ПолучитьЗначение<string>(_id_node, "Тип.Имя", КопироватьИз.хранилище, КопироватьИз.domain); //ПолучитьТипРаздела(_id_node, КопироватьИз.хранилище, КопироватьИз.domain);
        //        var values = new Dictionary<string, Value>();
        //        values.Add("GuidCode", new Value(ПолучитьЗначение<string>(_id_node, "GuidCode", КопироватьИз.хранилище, КопироватьИз.domain)));
        //        foreach (var item in new ConfigurationClient().СписокАтрибутов(ТипДанных, КопироватьИз.domain))
        //        {
        //            if (item.IsReadOnly) continue;
        //            if (КопироватьИз.domain == КопироватьВ.domain && item.IsAutoIncrement) continue;
        //            if (item.MemberType == MemberTypes.Таблица) 
        //                continue;

        //            //item.Name == "ДатаСозданияОбъекта" || item.Name == "РедакторРаздела"
        //            if (item.Name == "@РодительскийРаздел") 
        //                continue;

        //            var attrValue = item.Name;
        //            if (item.MemberType == MemberTypes.Ссылка)
        //                attrValue = item.Name + "/GuidCode";

        //            var _value = ПолучитьЗначение<object>(_id_node, attrValue, КопироватьИз.хранилище, КопироватьИз.domain);
        //            if (_value is DataTable)
        //                continue;
        //            else
        //                values.Add(item.Name, new Value(_value));
        //        }

        //        var id_node = ДобавитьРаздел(_id_parent, ТипДанных, values, false, КопироватьВ.хранилище, user, КопироватьВ.domain);
        //        var query = new RosService.Data.Query();
        //        query.ДобавитьМестоПоиска(_id_node, 1);
        //        foreach (var item in Поиск(query, КопироватьИз.хранилище, КопироватьИз.domain).AsEnumerable())
        //            copy(id_node, item.Field<decimal>("id_node"));

        //        return id_node;
        //    };
        //    return copy(Convert.ToDecimal(КопироватьВ.node), Convert.ToDecimal(КопироватьИз.node));
        //}


        public void ОтправитьПисьмо(string Кому, string Тема, string Содержание, Файл[] СписокФайлов, bool IsBodyHtml, string user, string domain)
        {
            ОтправитьПисьмо(Кому, Тема, Содержание, СписокФайлов, false, IsBodyHtml, user, domain);
        }
        public void ОтправитьПисьмо(string Кому, string Тема, string Содержание, Файл[] СписокФайлов, bool Срочно, bool IsBodyHtml, string user, string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(Кому))
                    {
                        Кому = Convert.ToString(ПолучитьКонстанту("Почта.Переадресация", domain));
                        if (string.IsNullOrEmpty(Кому)) throw new Exception("Не определено политика переадресации почты '//Константы/Почта.Переадресация'");
                    }

                    var УчетнаяЗапись = 0m;
                    //ищем учетную запись у пользователя
                    var query = new Query();
                    query.Типы.Add("Пользователь%");
                    query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ЛогинПользователя", Значение = user, Оператор = Query.Оператор.Равно });
                    var users = Поиск(query, Хранилище.Оперативное, domain).AsEnumerable();
                    if (users.Count() != 1) throw new Exception("При отправки электронного письма найдено более одного пользователя");

                    query = new Query();
                    query.Типы.Add("УчетнаяЗаписьПочты");
                    query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = users.First().Field<decimal>("id_node"), МаксимальнаяГлубина = 0 });
                    var accounts = Поиск(query, Хранилище.Оперативное, domain).AsEnumerable();
                    if (accounts.Count() > 0)
                    {
                        УчетнаяЗапись = accounts.First().Field<decimal>("id_node");
                    }
                    else
                    {
                        //если учётную запись не нашли у пользователя пробуем найти учетку по-умолчанию
                        //УчетнаяЗапись = ПолучитьЗначение<decimal>(0, "//Константы/Почта.УчетнаяЗапись.ПоУмолчанию", Хранилище.Оперативное, domain);
                        УчетнаяЗапись = Convert.ToDecimal(ПолучитьКонстанту("Почта.УчетнаяЗапись.ПоУмолчанию", domain));
                    }
                    if (УчетнаяЗапись == 0)
                        throw new Exception("Не определена учетная запись электронной почты по-умолчанию '//Константы/Почта.УчетнаяЗапись.ПоУмолчанию'");

                    //отправить письмо
                    var smtpClient = new SmtpClient();
                    smtpClient.EnableSsl = true;
                    smtpClient.Port = 587;
                    smtpClient.Host = ПолучитьЗначение<string>(УчетнаяЗапись, "Smtp", Хранилище.Оперативное, domain);
                    smtpClient.Credentials = new System.Net.NetworkCredential(
                        ПолучитьЗначение<string>(УчетнаяЗапись, "ЛогинПользователя", Хранилище.Оперативное, domain),
                        ПолучитьЗначение<string>(УчетнаяЗапись, "ПарольПользователя", Хранилище.Оперативное, domain));


                    var oMsg = new MailMessage()
                    {
                        IsBodyHtml = IsBodyHtml,
                        From = new MailAddress(
                            ПолучитьЗначение<string>(УчетнаяЗапись, "Email", Хранилище.Оперативное, domain),
                            ПолучитьЗначение<string>(УчетнаяЗапись, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                            Encoding.UTF8),
                        BodyEncoding = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8,
                        Subject = Тема,
                        Body = Содержание,
                        Priority = Срочно ? MailPriority.High : MailPriority.Normal
                    };
                    if (СписокФайлов != null)
                    {
                        foreach (var item in СписокФайлов)
                        {
                            MemoryStream ms = new MemoryStream(item.Stream);
                            oMsg.Attachments.Add(new Attachment(ms, item.Name));
                        }
                    }
                    foreach (var item in Кому.Split(",;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                    {
                        if (string.IsNullOrEmpty(item)) continue;
                        oMsg.To.Add(item);
                    }
                    smtpClient.Send(oMsg);
                }
                catch (Exception ex)
                {
                    new ConfigurationClient().ЖурналСобытийДобавитьОшибку(ex.Message, ex.ToString(), "WebService", domain);
                }
            });
        }

        public void Проиндексировать(decimal id_node, Хранилище хранилище, string domain, bool async)
        {
            if (async)
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    Проиндексировать(id_node, хранилище, domain, true, async);
                });
            }
            else
            {
                Проиндексировать(id_node, хранилище, domain, true, async);
            }
        }
        public void Проиндексировать(decimal id_node, Хранилище хранилище, string domain, bool ПолнаяИндексация, bool async)
        {
            var HashCode = string.Empty;
            var Prefix = GetTablePrefix(хранилище);
            var id_proc = 0m;

            try
            {
                #region Разделы
                var items = new Dictionary<decimal, string>();
                var oRow = 0;
                if (async)
                {
                    id_proc = new ConfigurationClient().Процесс_СоздатьПроцесс("Индексирование Db", "Индексирование Db: " + хранилище.ToString(), "", "", domain);
                    СохранитьЗначениеПростое(id_proc, "СтатусПроцесса", "Сбор данных", false, Хранилище.Оперативное, "", domain);
                }

                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    Action<decimal, string> Проиндексировать = null;
                    Проиндексировать = delegate(decimal _id_node, string parentHash)
                    {
                        using (var dsTable = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                        {
                            try
                            {
                                if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
                                var command = (db.Connection as SqlConnection).CreateCommand();
                                command.CommandTimeout = 120;
                                command.CommandText = string.Format("select [id_node], [HashCode] from {0}tblNode WITH(NOLOCK) where [id_parent] = @id_node", Prefix);
                                command.Parameters.AddWithValue("@id_node", _id_node).SqlDbType = SqlDbType.Decimal;
                                new SqlDataAdapter(command).Fill(dsTable);
                            }
                            finally
                            {
                                db.Connection.Close();
                            }

                            if (dsTable != null && dsTable.Tables.Count > 0)
                            {
                                foreach (DataRow item in dsTable.Tables[0].Rows)
                                {
                                    var item_id_node = item.Field<decimal>("id_node");
                                    var _HashCode = string.Format("{0}{1}", parentHash, ConfigurationClient.GetHashKey(item_id_node));
                                    items.Add(item_id_node, _HashCode);

                                    if (async && ++oRow % 100 == 0)
                                    {
                                        new ConfigurationClient().Процесс_ОбновитьСостояниеПроцесса(id_proc, oRow, domain);
                                    }

                                    Проиндексировать(item_id_node, _HashCode);
                                }
                            }
                        }
                    };

                    if (id_node > 0)
                    {
                        //var node = ПолучитьРаздел(ПолучитьЗначение<decimal>(id_node, "@РодительскийРаздел", хранилище, domain), null, хранилище, domain);
                        var parentHashCode = ПолучитьЗначение<string>(ПолучитьЗначение<decimal>(id_node, "@РодительскийРаздел", хранилище, domain), "HashCode", хранилище, domain);
                        HashCode = string.Format("{0}{1}", parentHashCode, ConfigurationClient.GetHashKey(id_node));
                        items.Add(id_node, HashCode);
                    }
                    Проиндексировать(id_node, HashCode);
                }
                #endregion

                #region Сохранить
                oRow = 0;
                if (async) СохранитьЗначениеПростое(id_proc, "СтатусПроцесса", "Сохранение данных", false, Хранилище.Оперативное, "", domain);
                using (RosService.DataClasses.ClientDataContext db = new RosService.DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open)
                            db.Connection.Open();

                        if (ПолнаяИндексация)
                        {
                            var sb = new StringBuilder();
                            #region @РодительскийРаздел is NULL
                            sb.AppendFormat(@"
                --@РодительскийРаздел is NULL
                update {0}tblValueHref set 
	                [double_value] = N.id_parent
                from {0}tblValueHref V
                    left join {0}tblNode N on V.id_node = N.id_node 
                    where V.[type] = '@РодительскийРаздел' and V.[double_value] is null
                ", Prefix);
                            #endregion

                            #region Ссылки
                            sb.AppendFormat(@"
                --проиндексировать ссылки
                update {0}tblValueHref set 
	                [string_value_index] = O.[string_value_index]
                from {0}tblValueHref
	                inner join assembly_tblAttribute A on {0}tblValueHref.[type] = A.[Name]
	                left join {0}tblValueString O on (O.[id_node] = {0}tblValueHref.[double_value] and O.[type] = 'НазваниеОбъекта')
	                where A.[id_parent] = 0 and A.[MemberType] = 7 and {0}tblValueHref.[double_value] > 0
                ", Prefix);
                            #endregion

                            var command = (db.Connection as SqlConnection).CreateCommand();
                            command.CommandText = sb.ToString();
                            command.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                            command.ExecuteNonQuery();
                        }

                        var sql = string.Format("update {0}tblNode set [HashCode] = @HashCode where [id_node] = @id_node", Prefix);
                        foreach (var item in items)
                        {
                            var command = (db.Connection as SqlConnection).CreateCommand();
                            command.CommandText = sql;
                            command.Parameters.AddWithValue("@id_node", item.Key).SqlDbType = SqlDbType.Decimal;
                            command.Parameters.AddWithValue("@HashCode", item.Value).SqlDbType = SqlDbType.VarChar;
                            command.ExecuteNonQuery();

                            if (async && ++oRow % 100 == 0)
                            {
                                new ConfigurationClient().Процесс_ОбновитьСостояниеПроцесса(id_proc, oRow, domain);
                            }
                        }
                    }
                    finally
                    {
                        db.Connection.Close();
                    }
                }
                #endregion

                #region Очистить родительские разделы
                if (async) СохранитьЗначениеПростое(id_proc, "СтатусПроцесса", "Очистка кеша", false, Хранилище.Оперативное, "", domain);
                MemoryCache.RemoveAll(MemoryCache.Path(domain, хранилище, 0), "@РодительскийРаздел");
                #endregion

                if (async) new ConfigurationClient().Процесс_ЗавершитьПроцесс(id_proc, domain);
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(string.Format("Проиндексировать: {0}", ex.Message), "", domain, id_node, ex.ToString());
                if (async) new ConfigurationClient().Процесс_ОшибкаВПроцессе(id_proc, ex.Message, domain);
            }
        }
        #endregion
    }

    [Serializable]
    public class DebugException : Exception
    {
        public DebugException() { }
        public DebugException(string message) : base(message) { }
        public DebugException(string message, Exception inner) : base(message, inner) { }
        protected DebugException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
