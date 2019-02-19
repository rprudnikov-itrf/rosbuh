using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Intreface;
using System.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;
using RosService.DataClasses;
using RosService.Caching;
using System.Threading.Tasks;
using RosService.Data;
using RosService.Configuration;

namespace RosService
{
    public enum РежимРазбора
    {
        ПоУмолчанию,
        КешированиеДанныхСозданиеТаблицы,
        КешированиеДанныхЗаполнение,
    };
    public class QueryBuilder
    {
        private ConfigurationClient configuration;
        private DataClient data;

        public ClientDataContext db { get; private set; }
        public string domain { get; set; }
        public Хранилище хранилище { get; set; }
        public Dictionary<string, MemberTypes?> Параметры { get; private set; }
        internal Dictionary<string, QueryColumn> Колонки { get; set; }
        internal string[] Типы { get; set; }
        internal string Индексы { get; set; }


        public QueryBuilder(ClientDataContext db)
        {
            this.db = db;
            Initialize();
        }
        public QueryBuilder(ClientDataContext db, Хранилище хранилище, string domain)
        {
            this.db = db;
            this.хранилище = хранилище;
            this.domain = domain;
            Initialize();
        }
        private void Initialize()
        {
            configuration = new ConfigurationClient();
            data = new RosService.Data.DataClient();
        }

        /// <summary>
        /// Sql запрос
        /// </summary>
        public SqlCommand Parse(Query запрос, РежимРазбора режим)
        {
            var tablePrefix = DataClient.GetTablePrefix(хранилище);
            var command = (db.Connection as SqlConnection).CreateCommand();
            var __CacheName = null as string;

            #region Обычный запрос
            if (!запрос.IsSql)
            {
                this.Колонки = new Dictionary<string, QueryColumn>();
                this.Параметры = new Dictionary<string, MemberTypes?>();

                var oRow = 0;
                var tblContanier = "@nodes";
                var sb = new StringBuilder(5000);
                sb.AppendLine("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
                sb.AppendLine("SET NOCOUNT ON");
                sb.AppendLine("---------------------------------");

                #region КешированиеДанныхСозданиеТаблицы || КешированиеДанныхЗаполнение
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                {
                    if (string.IsNullOrEmpty(запрос.CacheName))
                        throw new Exception("Не задано имя для кеширования данных");

                    __CacheName = QueryColumn.ParseName(запрос.CacheName);
                    tblContanier = "tempdb..[" + QueryColumn.ParseName(domain) + "___" + __CacheName + "]";

                    sb.AppendLine("SET DEADLOCK_PRIORITY HIGH;");
                    sb.AppendLine();

                    if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                    {
                        //удалить таблицу с кешем
                        sb.AppendFormat("IF OBJECT_ID('{0}') IS NOT NULL\nDROP TABLE {0};\n", "cache_" + __CacheName);
                        sb.AppendLine("---------------------------------");
                    }

                    //добавить колонки по-умолчанию
                    if (запрос.ВыводимыеКолонки.FirstOrDefault(p => p.Атрибут == "@РодительскийРаздел") == null)
                    {
                        запрос.ВыводимыеКолонки.Insert(0, new Query.Колонка() { Атрибут = "@РодительскийРаздел" });
                    }
                    if (запрос.ВыводимыеКолонки.FirstOrDefault(p => p.Атрибут == "HashCode" || p.Атрибут == "@HashCode") == null)
                    {
                        запрос.ВыводимыеКолонки.Insert(0, new Query.Колонка() { Атрибут = "HashCode" });
                    }
                    if (запрос.ВыводимыеКолонки.FirstOrDefault(p => p.Атрибут == "GuidCode" || p.Атрибут == "@GuidCode") == null)
                    {
                        запрос.ВыводимыеКолонки.Insert(0, new Query.Колонка() { Атрибут = "GuidCode" });
                    }
                }
                #endregion

                #region Подготовка данных
                if (запрос.ВыводимыеКолонки != null)
                {
                    foreach (var item in запрос.ВыводимыеКолонки.Where(p => p.Функция == Query.ФункцияАгрегации.Sql))
                    {
                        item.Атрибут = запрос.ParseSql(item.Атрибут, domain);
                    }
                }

                ResolveTypes(запрос.Типы.Count > 0 ? запрос.Типы[0].TrimEnd(new char[] { '%' }) : "object", запрос.ВыводимыеКолонки, domain);
                if (запрос.УсловияПоиска.Count > 0)
                    ResolveAttributes(запрос.УсловияПоиска.Select(p => new Query.Колонка() { Атрибут = p.Атрибут }), domain);
                if (запрос.Сортировки.Count > 0)
                    ResolveAttributes(запрос.Сортировки.Where(p => p.Направление != Query.НаправлениеСортировки.Sql).Select(p => new Query.Колонка() { Атрибут = p.Атрибут }), domain);
                if (запрос.Группировки.Count > 0)
                    ResolveAttributes(запрос.Группировки.Select(p => new Query.Колонка() { Атрибут = p }), domain);

                if (запрос.Объединения != null)
                {
                    foreach (var item in запрос.Объединения)
                    {
                        var _UnionNode = QueryBuilder.ResolveIdNode(item.МестоПоиска.id_node, хранилище, domain, false);
                        if (!Колонки.ContainsKey(item.Атрибут))
                        {
                            Колонки.Add(item.Атрибут, new QueryColumn()
                            {
                                OriginalName = item.Атрибут,
                                Name = QueryColumn.ParseName(item.Атрибут),
                                Тип = configuration.ПолучитьТип(item.Атрибут, domain),
                                UnionNode = _UnionNode,
                                IsUnion = true
                            });
                        }
                        else
                        {
                            var c = Колонки[item.Атрибут];
                            c.UnionNode = _UnionNode;
                            c.IsUnion = true;
                        }
                    }
                }

                var aggrigate_column = Enumerable.Empty<Query.Колонка>();
                if (запрос.ВыводимыеКолонки != null)
                {
                    aggrigate_column = запрос.ВыводимыеКолонки.Where(p => p.Функция != Query.ФункцияАгрегации.None && p.Функция != Query.ФункцияАгрегации.Sql);
                    foreach (var item in aggrigate_column)
                    {
                        if (!Колонки.ContainsKey(item.Атрибут)) continue;
                        Колонки[item.Атрибут].Функция = item.Функция;
                    }
                }
                if (запрос.МестаПоиска == null || запрос.МестаПоиска.Count == 0)
                {
                    запрос.МестаПоиска = new List<Query.МестоПоиска>();
                    запрос.МестаПоиска.Add(new Query.МестоПоиска()
                    {
                        id_node = 0,
                        МаксимальнаяГлубина = 0
                    });
                }
                #endregion

                #region select
                #region declare @nodes table(
                sb.AppendLine("--СОЗДАЕМ ТАБЛИЦУ");
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                {
                    //if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                    //{
                    //    sb.AppendFormat("IF OBJECT_ID('{0}') IS NOT NULL\nDROP TABLE [{0}];\n", tblContanier);
                    //}

                    sb.AppendFormat(@"
---------------------------------
IF (@UpdatePagging = 1 and OBJECT_ID('{0}') IS NOT NULL) 
DROP TABLE {0};
---------------------------------
declare @IsTable int = OBJECT_ID('{0}')
IF @IsTable IS NULL
BEGIN
CREATE TABLE {0}(
	                    [id_row] [int] IDENTITY(1,1) PRIMARY KEY CLUSTERED,
	                    [id_node] [numeric](18, 0)
                  )", tblContanier);
                }
                else
                {
                    sb.Append(@"
declare @nodes table(
						[id_row] [int] IDENTITY(1,1) PRIMARY KEY CLUSTERED,
						[id_node] [numeric](18, 0)
				 )");
                }
                sb.AppendLine();
                sb.AppendLine(";with");
                sb.AppendLine();
                #endregion

                #region Типы данных
                if (запрос.Типы.Count > 0)
                {
                    var Types = new List<string>(4);
                    //using (var db2 = new RosService.DataClasses.ClientDataContext(domain))
                    {
                        var commandTypes = (db.Connection as SqlConnection).CreateCommand();
                        try
                        {
                            var sbTypes = new StringBuilder(1500);
                            sbTypes.AppendLine("set nocount on");
                            sbTypes.AppendLine("---------------------------------");
                            oRow = 0;
                            foreach (var item in запрос.Типы)
                            {
                                if (oRow++ > 0) sbTypes.AppendLine(@"union");
                                sbTypes.AppendLine(@"select [Name] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [MemberType] = 1 and [TypeHashCode] like");

                                if (item.EndsWith("%"))
                                    sbTypes.AppendFormat("\t(select [TypeHashCode] + '%' from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] = '{0}')\n", item.Substring(0, item.Length - 1));
                                else
                                    sbTypes.AppendFormat("\t(select [TypeHashCode] from assembly_tblAttribute WITH(NOLOCK) where [id_parent] = 0 and [Name] = '{0}')\n", item);
                            }
                            if (commandTypes.Connection.State != ConnectionState.Open)
                                commandTypes.Connection.Open();
                            commandTypes.CommandText = sbTypes.ToString();
                            var reader = commandTypes.ExecuteReader();
                            while (reader.Read())
                            {
                                Types.Add("'" + Convert.ToString(reader["Name"]) + "'");
                            }
                        }
                        catch (Exception ex)
                        {
                            ConfigurationClient.WindowsLog(ex.Message, "", domain, ex.ToString());
                        }
                        finally
                        {
                            commandTypes.Connection.Close();
                            Типы = Types.ToArray();

                            if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы && Типы.Length == 0)
                            {
                                throw new Exception("Не возможно кешировать запрос, укажите ТипыДанных");
                            }
                        }
                    }
                }
                else
                {
                    Типы = new string[0];
                }
                #endregion

                #region МестаПоиска
                sb.Append("\tnodes as (");
                oRow = 0;
                var id_node_ = 0m;

                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                {
                    sb.AppendFormat("(select [id_node], [type] from {0}tblNode WITH(NOLOCK)", tablePrefix);
                    if (Типы.Length == 1) sb.AppendFormat(" where [type] = {0}", Типы[0]);
                    else if (Типы.Length > 1) sb.AppendFormat(" where [type] in ({0})", string.Join(",", Типы));
                    sb.Append(")");
                }
                else
                {
                    foreach (var item in запрос.МестаПоиска)
                    {
                        #region ResolveIdNode
                        if (item != null && item.id_node != null && item.id_node is string)
                        {
                            //заменить параметр
                            if (((string)item.id_node).StartsWith("@"))
                                item.id_node = запрос.Параметры.FirstOrDefault(p => p.Имя == (string)item.id_node).Значение;

                            item.id_node = ResolveIdNode(item.id_node, хранилище, domain, false);
                        }
                        id_node_ = Convert.ToDecimal(item.id_node);
                        #endregion

                        var param = "@HashCode" + (++oRow).ToString();

                        #region искать вверх
                        if (item.МаксимальнаяГлубина == -2)
                        {
                            sb.AppendFormat("select [id_parent] 'id_node', [type] from {0}tblNode where id_node = {1} and id_parent > 0", tablePrefix, param);
                            if (Типы.Length == 1) sb.AppendFormat(" and [type] = {0}", Типы[0]);
                            else if (Типы.Length > 1) sb.AppendFormat(" and [type] in ({0})", string.Join(",", Типы));

                            sb.AppendLine();
                            sb.AppendLine("union all");
                            sb.AppendFormat("select N.id_parent 'id_node', N.[type] from {0}tblNode N inner join nodes on N.id_node = nodes.id_node where N.id_parent > 0", tablePrefix);

                            if (Типы.Length == 1) sb.AppendFormat(" and N.[type] = {0}", Типы[0]);
                            else if (Типы.Length > 1) sb.AppendFormat(" and N.[type] in ({0})", string.Join(",", Типы));

                            sb.AppendLine();

                            command.Parameters.AddWithValue(param, id_node_).SqlDbType = SqlDbType.Decimal;
                        }
                        #endregion
                        #region искать вниз
                        else
                        {
                            if (oRow > 1) sb.Append(" union ");

                            sb.AppendFormat("(select [id_node], [type] from {0}tblNode WITH(NOLOCK) /*where [hide] = @hide*/ ", tablePrefix);
                            if (Типы.Length > 0 || id_node_ > 0 || item.МаксимальнаяГлубина > 0) sb.Append(" where ");

                            if (Типы.Length == 1) sb.AppendFormat("/*and*/ [type] = {0}", Типы[0]);
                            else if (Типы.Length > 1) sb.AppendFormat("/*and*/ [type] in ({0})", string.Join(",", Типы));

                            //найти много разделов
                            if (id_node_ > 0 || item.МаксимальнаяГлубина != 0)
                            {
                                if (Типы.Length > 0) sb.Append(" and ");
                                if (item.МаксимальнаяГлубина == 1)
                                {
                                    sb.Append("/*and*/ [id_parent] = " + param);
                                    command.Parameters.AddWithValue(param, id_node_).SqlDbType = SqlDbType.Decimal;
                                }
                                else
                                {
                                    sb.Append("/*and*/ [HashCode] like " + param);

                                    var _hash = ConfigurationClient.GetHashCode(id_node_, item.МаксимальнаяГлубина, хранилище, domain);
                                    command.Parameters.AddWithValue(param, string.IsNullOrEmpty(_hash) ? "%" : _hash).SqlDbType = SqlDbType.VarChar;
                                }
                            }
                            sb.Append(")");
                        }
                        #endregion
                    }
                }
                sb.Append(@")");
                #endregion

                #region select as
                if (режим == РежимРазбора.ПоУмолчанию)
                {
                    foreach (var item in Колонки.Values)
                    {
                        var __УсловияПоиска = запрос.УсловияПоиска.Where(p => p.Атрибут == item.OriginalName);
                        if (!запрос.IsПоискПоСодержимому &&
                            __УсловияПоиска.Count() == 0 &&
                            запрос.Сортировки.FirstOrDefault(p => p.Атрибут == item.OriginalName) == null)
                        {
                            //добавить только участвующих в сортировке и поиске
                            if (!item.IsJoin || (item.IsJoin
                                && запрос.УсловияПоиска.FirstOrDefault(p => p.Атрибут.Contains(item.OriginalName + "/")) == null
                                && запрос.Сортировки.FirstOrDefault(p => p.Атрибут.Contains(item.OriginalName + "/")) == null))
                                continue;
                        }

                        #region where
                        //var where = new StringBuilder();
                        //if (item.Тип != null && !item.Тип.IsReadOnly)
                        //{
                        //    foreach (var cond in __УсловияПоиска)
                        //    {
                        //        where.Append(@" and ");
                        //        where.Append("(");
                        //        var param = string.Format("@{0}_{1}", QueryColumn.ParseName(cond.Атрибут), cond.Оператор);
                        //        var defaultValue = cond.Значение;

                        //        var MemberType = item.Тип.MemberType;
                        //        defaultValue = DefaultValue(MemberType, cond.Оператор, cond.Значение);

                        //        switch (cond.Оператор)
                        //        {
                        //            case Query.Оператор.Соодержит:
                        //            case Query.Оператор.СоодержитСлева:
                        //                if (item.Тип.MemberType == MemberTypes.Ссылка)
                        //                {
                        //                    if (cond.Значение is string)
                        //                        where.AppendFormat(@"[string_value_index] like {0}", param);
                        //                    else
                        //                        where.AppendFormat(@"{0} = 0 or ({0} <> 0 and [double_value] = {0})", item.Name, param);
                        //                }
                        //                else
                        //                {
                        //                    where.AppendFormat(@"{0} like {1}", ResolveWhereColumnInner(item, cond, запрос.ФорматДат), ResolveWhereValue(item.Тип.MemberType, cond, запрос.ФорматДат, param, defaultValue));
                        //                }
                        //                break;

                        //            case Query.Оператор.Функция:
                        //                where.AppendFormat(@"{0} {1}", ResolveWhereColumnInner(item, cond, запрос.ФорматДат), cond.Значение);
                        //                break;

                        //            default:
                        //                where.AppendFormat(@"{0} {2} {1}", ResolveWhereColumnInner(item, cond, запрос.ФорматДат), ResolveWhereValue(item.Тип.MemberType, cond, запрос.ФорматДат, param, defaultValue), ResolveOperator(cond.Оператор, defaultValue));
                        //                break;
                        //        }

                        //        where.Append(@")");

                        //        //добавить значение, если значение не определено заменить на значение по-умолчанию
                        //        if (!command.Parameters.Contains(param))
                        //        {
                        //            command.Parameters.AddWithValue(param, defaultValue ?? Convert.DBNull);
                        //        }
                        //        Параметры[param] = MemberType;
                        //    }
                        //}
                        #endregion

                        if (item.OriginalName == "HashCode" || item.OriginalName == "@HashCode" || item.OriginalName.EndsWith("/HashCode") || item.OriginalName.EndsWith("/@HashCode"))
                        {
                            sb.AppendFormat(",\n\t{1} as (select id_node, [HashCode] value from {0}tblNode WITH(NOLOCK) /*where [hide] = @hide*/)", tablePrefix, item.Name);
                        }
                        else if (item.OriginalName == "GuidCode" || item.OriginalName == "@GuidCode" || item.OriginalName.EndsWith("/GuidCode") || item.OriginalName.EndsWith("/@GuidCode"))
                        {
                            sb.AppendFormat(",\n\t{1} as (select id_node, [GuidCode] value from {0}tblNode WITH(NOLOCK) /*where [hide] = @hide*/)", tablePrefix, item.Name);
                        }
                        else if (item.Тип == null || item.Тип.RegisterType == RegisterTypes.undefined)
                        {
                            continue;
                        }
                        else if (item.Тип.IsReadOnly)
                        {
                            var RegisterType = item.Тип.RegisterType.ToString();
                            if (item.Тип.RegisterType == RegisterTypes.string_value)
                                RegisterType = "string_value_index";

                            sb.AppendFormat(",\n\t{0} as (select n.[type], v.[{2}] value from assembly_tblNode n WITH(NOLOCK) left join assembly_{4} v WITH(NOLOCK) on n.id_node = v.id_node where n.id_parent = {3} and v.[type] = '{1}')",
                                item.Name, item.Тип.Name, RegisterType, (int)ConfigurationClient.СистемныеПапки.РазделЗначения, DataClient.GetTableValue(item.Тип.MemberType));
                        }
                        else
                        {
                            var where = new StringBuilder();
                            if (item.UnionNode > 0)
                            {
                                var param = "@" + item.Name + "1";
                                where.Append(" and [id_node] = " + param);
                                command.Parameters.AddWithValue(param, item.UnionNode).SqlDbType = SqlDbType.Decimal;
                            }

                            switch (item.Тип.MemberType)
                            {
                                case MemberTypes.Ссылка:
                                    {
                                        if (!item.IsUnion)
                                            sb.AppendFormat(",\n\t{1} as (select [id_node], convert(numeric(18,0),[{3}]) value, [string_value_index] label from {0} WITH(NOLOCK) where /*[hide] = @hide and*/ [type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where);
                                        else
                                            sb.AppendFormat(",\n\t{1} as (select [id_node], convert(numeric(18,0),[{3}]) value from {0} WITH(NOLOCK) where /*[hide] = @hide and*/ [type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where);
                                    }
                                    break;

                                case MemberTypes.DateTime:
                                    {
                                        sb.AppendFormat(",\n\t{1} as (select [id_node], {3} value from {0} WITH(NOLOCK) where /*[hide] = @hide and*/ [type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name,
                                            string.Format(ResolveDateFormat(запрос.ФорматДат), item.Тип.RegisterType.ToString()), where);
                                    }
                                    break;


                                case MemberTypes.String:
                                    {
                                        sb.AppendFormat(",\n\t{1} as (select [id_node], [string_value_index] value from {0} WITH(NOLOCK) where /*[hide] = @hide and*/ [type] = '{2}'{3})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, where);
                                    }
                                    break;

                                default:
                                    {
                                        sb.AppendFormat(",\n\t{1} as (select [id_node], [{3}] value from {0} WITH(NOLOCK) where /*[hide] = @hide and*/ [type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where);
                                    }
                                    break;
                            }
                        }
                    }
                }
                #endregion

                #region insert into @nodes
                if (запрос.КоличествоВыводимыхСтраниц == 0 || запрос.КоличествоВыводимыхСтраниц == int.MaxValue)
                {
                    sb.AppendFormat(@"
insert into {0} (id_node)
select 
    nodes.[id_node]
from
    nodes WITH(NOLOCK)", tblContanier);
                }
                else
                {
                    sb.AppendFormat(@"
insert into {0} (id_node)
select top(cast(@CountSize as int))
    nodes.[id_node]
from
    nodes WITH(NOLOCK)", tblContanier);
                }
                #endregion
                #endregion

                #region join
                if (режим == РежимРазбора.ПоУмолчанию)
                {
                    foreach (var item in Колонки.Values.OrderBy(p => p.IsPath))
                    {
                        var __УсловияПоиска = запрос.УсловияПоиска.Where(p => p.Атрибут == item.OriginalName);
                        if (!запрос.IsПоискПоСодержимому &&
                            __УсловияПоиска.Count() == 0 &&
                            запрос.Сортировки.FirstOrDefault(p => p.Атрибут == item.OriginalName) == null)
                        {
                            //if (!item.IsJoin) continue;

                            //добавить только участвующих в сортировке и поиске
                            if (!item.IsJoin || (item.IsJoin
                                && запрос.УсловияПоиска.FirstOrDefault(p => p.Атрибут.Contains(item.OriginalName + "/")) == null
                                && запрос.Сортировки.FirstOrDefault(p => p.Атрибут.Contains(item.OriginalName + "/")) == null))
                                continue;
                        }

                        var joinTag = "left"; //__УсловияПоиска.Count() > 0 ? "inner" : "left";
                        if (item.IsPath)
                        {
                            sb.AppendFormat("\n\t{2} join {0} WITH(NOLOCK) on {1}.[value] = {0}.[id_node]", item.Name, item.Path, joinTag);
                        }
                        else if (item.IsUnion)
                        {
                            sb.AppendFormat("\n\t{1} join {0} WITH(NOLOCK) on {0}.[value] = nodes.[id_node]", item.Name, joinTag);
                        }
                        else if (item.Тип != null && item.Тип.MemberType != MemberTypes.Undefined)
                        {
                            if (item.Тип.IsReadOnly)
                                sb.AppendFormat("\n\t{1} join {0} WITH(NOLOCK) on nodes.[type] = {0}.[type]", item.Name, joinTag);
                            else
                                sb.AppendFormat("\n\t{1} join {0} WITH(NOLOCK) on nodes.[id_node] = {0}.[id_node]", item.Name, joinTag);
                        }
                    }
                    if (запрос.УсловияПоиска != null)
                    {
                        foreach (var item in запрос.УсловияПоиска.Where(p => p.Оператор == Query.Оператор.Функция && string.IsNullOrEmpty(p.Атрибут)))
                        {
                            sb.AppendFormat("\n\t{0}", item.Значение);
                        }
                    }
                }
                #endregion

                #region where
                if (режим == РежимРазбора.ПоУмолчанию && запрос.УсловияПоиска != null)
                {
                    var wb = new StringBuilder();
                    oRow = 0;

                    foreach (var item in запрос.УсловияПоиска.Where(p => !string.IsNullOrEmpty(p.Атрибут)))
                    {
                        //если при полном поиске значение не задано, упращаем запрос
                        if (item.Атрибут == "*" && !запрос.IsПоискПоСодержимому) continue;

                        if (oRow > 0) wb.AppendLine(@" and ");
                        wb.Append("\t(");
                        var param = string.Format("@p{0}", ++oRow);
                        var MemberType = (MemberTypes?)null;
                        var defaultValue = item.Значение;

                        #region item.Атрибут == "*"
                        if (item.Атрибут == "*")
                        {
                            //if (oRow > 0) wb.AppendLine(@" and ");
                            //wb.Append("\t(");
                            var oColumn = 0;
                            foreach (var column in Колонки.Values)
                            {
                                if (column.Тип == null) continue;

                                if (!(column.Тип.MemberType == MemberTypes.String ||
                                      column.Тип.MemberType == MemberTypes.Ссылка ||
                                      column.Тип.MemberType == MemberTypes.Int ||
                                      column.Тип.MemberType == MemberTypes.Double)) continue;
                                if (oColumn++ > 0) wb.Append(" or \n\t\t");

                                switch (column.Тип.MemberType)
                                {
                                    case MemberTypes.String:
                                    case MemberTypes.Double:
                                    case MemberTypes.Int:
                                        wb.AppendFormat(@"{0}.[value] like {1}", column.Name, param);
                                        break;

                                    case MemberTypes.Ссылка:
                                        wb.AppendFormat(@"{0}.[label] like {1}", column.Name, param);
                                        break;
                                }
                            }
                            //wb.Append(")");
                        }
                        #endregion
                        #region item.Атрибут == "id_node"
                        else if (item.Атрибут == "id_node")
                        {
                            //if (oRow > 0) wb.AppendLine(@" and ");
                            //wb.Append("\t(");
                            switch (item.Оператор)
                            {
                                case Query.Оператор.Функция:
                                    wb.AppendFormat(@"nodes.id_node {0}", item.Значение);
                                    break;

                                default:
                                    if (defaultValue == null) defaultValue = 0m;
                                    wb.AppendFormat(@"nodes.id_node {1} {0}", ResolveWhereValue(MemberTypes.Double, item, запрос.ФорматДат, param, defaultValue), ResolveOperator(item.Оператор, defaultValue));
                                    break;
                            }
                            //wb.Append(")");
                        }
                        #endregion
                        #region item.Оператор = Sql
                        else if (item.Оператор == Query.Оператор.Sql && string.IsNullOrEmpty(item.Атрибут))
                        {
                            wb.Append(item.Значение);
                        }
                        #endregion
                        #region else
                        else
                        {
                            if (!Колонки.ContainsKey(item.Атрибут)) continue;

                            var id_attr = Колонки[item.Атрибут];
                            if (id_attr == null || id_attr.Тип == null /*|| id_attr.Тип.RegisterType == RegisterTypes.undefined*/) continue;

                            MemberType = id_attr.Тип.MemberType;
                            defaultValue = DefaultValue(MemberType, item.Оператор, item.Значение);

                            switch (item.Оператор)
                            {
                                case Query.Оператор.Соодержит:
                                case Query.Оператор.СоодержитСлева:
                                case Query.Оператор.СоодержитСправа:
                                    if (id_attr.Тип.MemberType == MemberTypes.Ссылка)
                                    {
                                        if (item.Значение is string)
                                            wb.AppendFormat(@"{0}.[label] like {1}", id_attr.Name, param);
                                        else
                                            wb.AppendFormat(@"({1} <> 0 and {0}.[value] = {1}) or {1} = 0", id_attr.Name, param);
                                    }
                                    else
                                    {
                                        wb.AppendFormat(@"{0} like {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, false), ResolveWhereValue(id_attr.Тип.MemberType, item, запрос.ФорматДат, param, defaultValue));
                                    }
                                    break;

                                case Query.Оператор.Функция:
                                    wb.AppendFormat(@"{0} {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, false), Convert.ToString(item.Значение).Replace("|", ","));
                                    break;

                                case Query.Оператор.Sql:
                                    wb.AppendFormat(Convert.ToString(item.Значение).Replace("|", ","), ResolveWhereColumn(id_attr, item, запрос.ФорматДат, false));
                                    break;

                                default:
                                    wb.AppendFormat(@"{0} {2} {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, false), ResolveWhereValue(id_attr.Тип.MemberType, item, запрос.ФорматДат, param, defaultValue), ResolveOperator(item.Оператор, defaultValue));
                                    break;
                            }
                        }
                        #endregion

                        wb.Append(@")");

                        //добавить значение, если значение не определено заменить на значение по-умолчанию
                        if (!command.Parameters.Contains(param))
                        {
                            AddWithValue(command, param, defaultValue ?? Convert.DBNull /*, MemberType*/);
                        }
                        Параметры[param] = MemberType ?? MemberTypes.String;
                    }

                    if (wb.Length > 0)
                    {
                        sb.AppendLine("");
                        sb.AppendLine(@"where");
                        sb.Append(wb.ToString());
                    }
                }
                #endregion

                #region order by
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                {
                    sb.AppendLine("\norder by [id_node] desc");
                }
                else if (режим == РежимРазбора.ПоУмолчанию && запрос.Сортировки != null && запрос.Сортировки.Count > 0)
                {
                    oRow = 0;
                    var sort = new StringBuilder();
                    foreach (var column in запрос.Сортировки)
                    {
                        #region Sql
                        if (column.Направление == Query.НаправлениеСортировки.Sql)
                        {
                            if (oRow++ > 0) sort.Append(",\n");

                            sort.Append("\t");
                            sort.Append(column.Атрибут);
                        }
                        #endregion
                        #region else
                        else
                        {
                            if (!Колонки.ContainsKey(column.Атрибут)) continue;
                            var item = Колонки[column.Атрибут];
                            if (item.Тип == null) continue;

                            if (oRow++ > 0) sort.Append(",\n");

                            switch (column.Направление)
                            {
                                case Query.НаправлениеСортировки.Rand:
                                    sort.Append("\tNEWID()");
                                    break;

                                default:
                                    switch (item.Тип.MemberType)
                                    {
                                        case MemberTypes.Ссылка:
                                            sort.AppendFormat("\t{0}.[label] {1}", item.Name, column.Направление.ToString());
                                            break;

                                        default:
                                            sort.AppendFormat("\t" + ResolveConverter(item, false) + " {1}", item.Name, column.Направление.ToString());
                                            break;
                                    }
                                    break;
                            }
                        }
                        #endregion
                    }

                    if (sort.Length > 0)
                    {
                        sb.AppendLine("\norder by");
                        sb.Append(sort.ToString());
                    }
                }
                #endregion

                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                {
                    sb.AppendLine();
                    sb.Append(@"END");
                }

                /////////////////////////////////////////////////////////////

                #region select
                #region РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ
                sb.Append(@"
--END МАССИВ ОПРЕДЕЛЕНИЯ ВЫБОРКИ СТРАНИЦ И СОРТИРОВКИ

--РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ
declare @TotalRows decimal(18,0) = @@Rowcount
");

                sb.Append(@"
declare @TotalPage  decimal(18,0) = @CurrentPage * @PageSize;
declare @PageCount decimal(18,0) = case when @PageSize = 0 then 0 else ceiling(@TotalRows / @PageSize) end;
--END РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ

declare @Max  decimal(18,0) = 0;
declare @Min  decimal(18,0) = 0;
");
                #endregion

                sb.AppendFormat("select @Max = max(id_node), @Min = min(id_node) from {0}{1} where [id_row] > @TotalPage and [id_row] <= @TotalPage + @PageSize", tblContanier, tblContanier != "@nodes" ? " WITH(NOLOCK)" : "");
                sb.AppendLine();
                sb.AppendLine();

                sb.AppendLine(";with");
                sb.AppendFormat("\tnodes as (select top(cast(@PageSize as int)) * from {0}{1} where [id_row] > @TotalPage and [id_row] <= @TotalPage + @PageSize),", tblContanier, tblContanier != "@nodes" ? " WITH(NOLOCK)" : "");
                sb.AppendLine();
                sb.AppendFormat("\ttypes as (select id_node, [type] value from {0}tblNode WITH(NOLOCK))", tablePrefix);

                oRow = 0;
                foreach (var item in Колонки.Values)
                {
                    if (item.OriginalName == "HashCode" || item.OriginalName == "@HashCode" || item.OriginalName.EndsWith("/HashCode") || item.OriginalName.EndsWith("/@HashCode"))
                    {
                        sb.AppendFormat(",\n\t{1} as (select id_node, [HashCode] value from {0}tblNode WITH(NOLOCK))", tablePrefix, item.Name);
                    }
                    else if (item.OriginalName == "GuidCode" || item.OriginalName == "@GuidCode" || item.OriginalName.EndsWith("/GuidCode") || item.OriginalName.EndsWith("/@GuidCode"))
                    {
                        sb.AppendFormat(",\n\t{1} as (select id_node, [GuidCode] value from {0}tblNode WITH(NOLOCK))", tablePrefix, item.Name);
                    }
                    else if (item.Тип == null || item.Тип.RegisterType == RegisterTypes.undefined)
                    {
                        continue;
                    }
                    else if (item.Тип.IsReadOnly)
                    {
                        var RegisterType = item.Тип.RegisterType.ToString();
                        if (item.Тип.RegisterType == RegisterTypes.string_value)
                            RegisterType = "string_value_index";

                        sb.AppendFormat(",\n\t{0} as (select n.[type], v.[{2}] value from assembly_tblNode n WITH(NOLOCK) left join assembly_{4} v WITH(NOLOCK) on n.id_node = v.id_node where n.id_parent = {3} and v.[type] = '{1}')",
                            item.Name, item.Тип.Name, RegisterType, (int)ConfigurationClient.СистемныеПапки.РазделЗначения, DataClient.GetTableValue(item.Тип.MemberType));
                    }
                    else
                    {
                        //при объединениях добавляем ограничение по id_node
                        var where = string.Empty;
                        if (item.UnionNode > 0)
                        {
                            var param = "@" + item.Name + "2";
                            where = string.Format(" and [id_node] = {0}", param);
                            command.Parameters.AddWithValue(param, item.UnionNode).SqlDbType = SqlDbType.Decimal;
                        }

                        var between = "([id_node] between @min and @max) and ";
                        switch (item.Тип.MemberType)
                        {
                            case MemberTypes.Ссылка:
                                if (!item.IsUnion)
                                    sb.AppendFormat(",\n\t{1} as (select id_node, convert(numeric(18,0),[{3}]) value, [string_value_index] label from {0} WITH(NOLOCK) where [type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where);
                                else
                                    sb.AppendFormat(",\n\t{1} as (select id_node, convert(numeric(18,0),[{3}]) value from {0} WITH(NOLOCK) where {5}[type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where,
                                        item.IsPath && item.OriginalName.Contains('/') ? "" : between);
                                break;

                            case MemberTypes.DateTime:
                                sb.AppendFormat(",\n\t{1} as (select id_node, {3} value from {0} WITH(NOLOCK) where {5}[type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name,
                                    string.Format(ResolveDateFormat(запрос.ФорматДат), item.Тип.RegisterType.ToString()), where,
                                        item.IsPath && item.OriginalName.Contains('/') ? "" : between);
                                break;

                            case MemberTypes.String:
                                if (item.ПолнотекстовыйВывод != null && item.ПолнотекстовыйВывод.Value)
                                    sb.AppendFormat(",\n\t{1} as (select id_node, [string_value] value from {0} WITH(NOLOCK) where  {4}[type] = '{2}'{3})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, where,
                                        item.IsPath && item.OriginalName.Contains('/') ? "" : between);
                                else
                                    sb.AppendFormat(",\n\t{1} as (select id_node, [string_value_index] value from {0} WITH(NOLOCK) where {4}[type] = '{2}'{3})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, where,
                                        item.IsPath && item.OriginalName.Contains('/') ? "" : between);
                                break;

                            default:
                                sb.AppendFormat(",\n\t{1} as (select id_node, [{3}] value from {0} WITH(NOLOCK) where {5}[type] = '{2}'{4})", tablePrefix + DataClient.GetTableValue(item.Тип.MemberType), item.Name, item.Тип.Name, item.Тип.RegisterType.ToString(), where,
                                    item.IsPath && item.OriginalName.Contains('/') ? "" : between);
                                break;
                        }
                    }
                    oRow++;
                }
                sb.AppendLine();

                var IsAggregate = aggrigate_column.Count() > 0 || запрос.Группировки.Count > 0;
                if (!IsAggregate)
                {
                    #region Вывод стандартный
                    sb.AppendLine("select");
                    if (режим == РежимРазбора.ПоУмолчанию)
                        sb.AppendLine("\tnodes.[id_row] 'НомерСтроки',");
                    sb.AppendLine("\tnodes.[id_node] 'id_node',");
                    sb.Append("\ttypes.[value] 'type'");

                    foreach (var item in Колонки.Values.Where(p => !p.IsHide))
                    {
                        if (item.Функция == Query.ФункцияАгрегации.Sql)
                        {
                            sb.AppendFormat(",\n\t{0}", item.OriginalName);
                        }
                        else if (item.Тип != null && item.Тип.MemberType != MemberTypes.Undefined)
                        {
                            sb.AppendFormat(",\n\t" + ResolveConverter(item, false) + " '{1}'", item.Name, item.OriginalName);
                            if (item.Тип.MemberType == MemberTypes.Ссылка && !item.IsUnion)
                            {
                                sb.AppendFormat(",\n\tisnull({0}.[label],'') '{1}.НазваниеОбъекта'", item.Name, item.OriginalName);
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы || режим == РежимРазбора.КешированиеДанныхЗаполнение)
                    {
                        throw new Exception("Кеширование запросов с агрегацией невозможно");
                    }

                    #region Вывод с группировками
                    sb.Append("select\n");
                    oRow = 0;

                    foreach (var g in запрос.Группировки)
                    {
                        var item = Колонки[g];
                        if (item == null) continue;

                        if (oRow++ > 0) sb.Append(",");
                        sb.AppendFormat("\n\t{0}.[value] 'GROUP{1}'", item.Name, oRow);
                    }

                    foreach (var column in aggrigate_column.Select(p => p.Атрибут).
                        Union(запрос.ВыводимыеКолонки.Select(p => p.Атрибут)))
                    {
                        var item = Колонки[column];
                        switch (item.Функция)
                        {
                            case Query.ФункцияАгрегации.None:
                                {
                                    //если в запросе нет атрибута то не выводить его
                                    if (запрос.Группировки != null && !запрос.Группировки.Contains(item.OriginalName))
                                        continue;

                                    if (item.Тип.RegisterType == RegisterTypes.undefined) continue;
                                    switch (item.Тип.MemberType)
                                    {
                                        case MemberTypes.Ссылка:
                                            if (oRow++ > 0) sb.Append(",");
                                            sb.AppendFormat("\n\tisnull({0}.[value], 0) '{1}',", item.Name, item.OriginalName);
                                            sb.AppendFormat("\n\tisnull({0}.[label], '') '{1}.НазваниеОбъекта'", item.Name, item.OriginalName);
                                            break;

                                        default:
                                            if (oRow++ > 0) sb.Append(",");
                                            sb.AppendFormat("\n\t" + ResolveConverter(item, false) + " '{1}'", item.Name, item.OriginalName);
                                            break;
                                    }
                                }
                                break;

                            case Query.ФункцияАгрегации.Sql:
                            case Query.ФункцияАгрегации.Функция:
                                if (oRow++ > 0) sb.Append(",");
                                sb.AppendFormat("\n\t{0}", item.OriginalName);
                                break;

                            case Query.ФункцияАгрегации.Distinct:
                                if (oRow++ > 0) sb.Append(",");
                                sb.AppendFormat("\n\t{2}({0}.[value]) '{1}'", item.Name, item.OriginalName, item.Функция.ToString());
                                break;

                            default:
                                if (oRow++ > 0) sb.Append(",");
                                sb.AppendFormat("\n\tisnull({2}({0}.[value]), 0) '{1}'", item.Name, item.OriginalName, item.Функция.ToString());
                                break;
                        }
                    }
                    #endregion
                }
                #endregion

                #region join
                sb.AppendLine();
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                {
                    sb.AppendFormat("into {0}\n", "cache_" + __CacheName);
                }
                sb.AppendLine("from");
                sb.AppendLine("\tnodes WITH(NOLOCK)");
                sb.Append("\tleft join types WITH(NOLOCK) on nodes.[id_node] = types.[id_node]");
                foreach (var item in Колонки.Values.Where(p => !p.IsHide || p.IsJoin).OrderBy(p => p.IsPath))
                {
                    if (item == null /*|| (запрос.ВыводимыеКолонки.FirstOrDefault(p => p.Атрибут == item.OriginalName) == null && !item.IsJoin)*/)
                    {
                        continue;
                    }
                    else if (item.IsPath)
                    {
                        sb.AppendFormat("\n\tleft join {0} WITH(NOLOCK) on {1}.[value] = {0}.[id_node]", item.Name, item.Path);
                    }
                    else if (item.IsUnion)
                    {
                        sb.AppendFormat("\n\tleft join {0} WITH(NOLOCK) on {0}.[value] = nodes.[id_node]", item.Name);
                    }
                    else if (item.Тип != null && item.Тип.MemberType != MemberTypes.Undefined)
                    {
                        if (item.Тип.IsReadOnly)
                            sb.AppendFormat("\n\tleft join {0} WITH(NOLOCK) on types.[value] = {0}.[type]", item.Name);
                        else
                            sb.AppendFormat("\n\tleft join {0} WITH(NOLOCK) on nodes.[id_node] = {0}.[id_node]", item.Name);
                    }
                }
                #endregion

                #region group by
                if (запрос.Группировки.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(@"group by");
                    oRow = 0;
                    foreach (var item in запрос.Группировки)
                    {
                        if (oRow++ > 0) sb.Append(",");

                        var column = Колонки[item];
                        switch (column.Тип.MemberType)
                        {
                            case MemberTypes.Ссылка:
                                sb.AppendFormat("\n\t{0}.[label], {0}.[value]", column.Name);
                                break;
                            default:
                                sb.AppendFormat("\n\t{0}.[value]", column.Name);
                                break;
                        }
                    }
                    sb.AppendLine();
                }
                #endregion

                #region order by
                else if (запрос.Сортировки.Count > 0 && !IsAggregate)
                {
                    sb.AppendLine("\norder by");
                    sb.AppendLine("\tid_row asc");
                }


                if (режим == РежимРазбора.ПоУмолчанию || режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                {
                    sb.AppendLine();
                    sb.Append("select isnull(@TotalRows, 0) as TotalRows, isnull(@PageCount, 0) as PageCount");
                }
                #endregion

                #region params
                foreach (var item in запрос.Параметры)
                {
                    if (command.Parameters.Contains(item.Имя))
                        continue;
                    AddWithValue(command, item.Имя, item.Значение ?? string.Empty);
                }
                #endregion

                #region Кеширование, добавить индексы
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                {
                    var indexString = new StringBuilder();
                    #region индыксы по-умолчанию
                    indexString.AppendFormat(@"
ALTER TABLE cache_{0} ADD CONSTRAINT
	PK_cache_{0} PRIMARY KEY NONCLUSTERED 
	([id_node]) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

--добавить первичный ключ
CREATE CLUSTERED INDEX [IX_cache_{0}_id_node] ON [cache_{0}]
    ([id_node]) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 80) ON [PRIMARY]

--добавить индексные поля
CREATE NONCLUSTERED INDEX [IX_cache_{0}_type] ON [cache_{0}]
    ([type]) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, PAD_INDEX = ON, FILLFACTOR = 80) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [IX_cache_{0}] ON [cache_{0}]
    ([type],[id_node]) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, PAD_INDEX = ON, FILLFACTOR = 80) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [IX_cache_{0}_HashCode] ON [cache_{0}]
    ([HashCode]) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, PAD_INDEX = ON, FILLFACTOR = 80) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [IX_cache_{0}_GuidCode] ON [cache_{0}]
    ([GuidCode]) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, PAD_INDEX = ON, FILLFACTOR = 80) ON [PRIMARY]
", __CacheName, tablePrefix);
                    #endregion

                    foreach (var item in Колонки.Values)
                    {
                        if (item.Тип == null || item.Тип.RegisterType == RegisterTypes.undefined)
                            continue;
                        else if (item.ПолнотекстовыйВывод != null && item.ПолнотекстовыйВывод.Value)
                            continue;

                        indexString.AppendFormat(@"
--{1}
CREATE NONCLUSTERED INDEX [IX_cache_{0}_{2}] ON [cache_{0}]
    ([{3}] ASC) WITH (SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, PAD_INDEX = ON, FILLFACTOR = 80) ON [PRIMARY]
", __CacheName, item.OriginalName, item.Name, (item.OriginalName ?? "").Replace("]", "]]"));
                    }

                    Индексы = indexString.ToString();
                }
                #endregion

                command.CommandText = sb.ToString();
            }
            #endregion

            #region Запрос Sql
            else
            {
                if (режим == РежимРазбора.КешированиеДанныхСозданиеТаблицы)
                    throw new Exception("Sql запрос не может быть кеширован");

                var node = запрос.Параметры.FirstOrDefault(p => p.Имя == "@id_node");
                if (node != null)
                {
                    AddWithValue(command, "@id_node", ConfigurationClient.GetHashCode(Convert.ToDecimal(node.Значение), 0, хранилище, domain));
                }
                foreach (var item in запрос.Параметры)
                {
                    if (item.Имя == "@id_node") continue;
                    AddWithValue(command, item.Имя, item.Значение ?? "");
                }

                //распарсить запрос
                command.CommandText = string.Format(запрос.ParseSql(запрос.Sql, domain), tablePrefix);
            }
            #endregion

            return command;
        }
        /// <summary>
        /// Sql запрос из Cache
        /// </summary>
        public SqlCommand ParseFromCache(Query запрос, КешЗапрос _cache)
        {
            var command = (db.Connection as SqlConnection).CreateCommand();
            var oRow = 0;

            #region МестаПоиска
            if (запрос.МестаПоиска == null || запрос.МестаПоиска.Count == 0)
            {
                запрос.МестаПоиска = new List<Query.МестоПоиска>();
                запрос.МестаПоиска.Add(new Query.МестоПоиска()
                {
                    id_node = 0,
                    МаксимальнаяГлубина = 0
                });
            }
            foreach (var item in запрос.МестаПоиска)
            {
                if (item != null && item.id_node != null && item.id_node is string)
                {
                    if (((string)item.id_node).StartsWith("@"))
                        item.id_node = запрос.Параметры.FirstOrDefault(p => p.Имя == (string)item.id_node).Значение;

                    item.id_node = ResolveIdNode(item.id_node, хранилище, domain, false);
                }
                oRow++;

                var id_node_ = Convert.ToDecimal(item.id_node);
                //найти много разделов
                if (id_node_ > 0 || item.МаксимальнаяГлубина != 0)
                {
                    var param = "@HashCode" + oRow.ToString();
                    if (item.МаксимальнаяГлубина == -2)
                    {
                        command.Parameters.AddWithValue(param, id_node_).SqlDbType = SqlDbType.Decimal;
                    }
                    else if (item.МаксимальнаяГлубина == 1)
                    {
                        command.Parameters.AddWithValue(param, id_node_).SqlDbType = SqlDbType.Decimal;
                    }
                    else
                    {
                        var _hash = ConfigurationClient.GetHashCode(id_node_, item.МаксимальнаяГлубина, хранилище, domain);
                        command.Parameters.AddWithValue(param, string.IsNullOrEmpty(_hash) ? "%" : _hash).SqlDbType = SqlDbType.VarChar;
                    }
                }
            }
            #endregion

            #region where
            if (запрос.УсловияПоиска != null)
            {
                oRow = 0;
                foreach (var item in запрос.УсловияПоиска.Where(p => !string.IsNullOrEmpty(p.Атрибут)))
                {
                    //если при полном поиске значение не задано, упращаем запрос
                    if (item.Атрибут == "*" && !_cache.IsПоискПоСодержимому)
                        continue;

                    var param = string.Format("@p{0}", ++oRow);
                    if (!command.Parameters.Contains(param) && _cache.Параметры.ContainsKey(param))
                    {
                        //command.Parameters.AddWithValue(param, QueryBuilder.DefaultValue(_cache.Параметры[param], item.Оператор, item.Значение) ?? Convert.DBNull);

                        var member = _cache.Параметры[param];
                        if (member.HasValue && member.Value == MemberTypes.Ссылка && item.Значение is string
                            && item.Оператор == Query.Оператор.Равно)
                        {
                            item.Значение = ResolveIdNode(item.Значение, хранилище, domain, false);
                        }
                        AddWithValue(command, param, QueryBuilder.DefaultValue(member, item.Оператор, item.Значение) ?? Convert.DBNull /*, _cache.Параметры[param]*/);
                    }
                }
            }
            #endregion

            #region params
            if (запрос.Параметры != null)
            {
                foreach (var item in запрос.Параметры)
                {
                    if (command.Parameters.Contains(item.Имя)) continue;
                    //command.Parameters.AddWithValue(item.Имя, item.Значение ?? string.Empty);
                    AddWithValue(command, item.Имя, item.Значение ?? string.Empty);
                }
            }
            #endregion

            #region union
            if (запрос.Объединения != null)
            {
                foreach (var item in запрос.Объединения)
                {
                    var _UnionNode = item.МестоПоиска.id_node is string
                        ? data.ПоискРазделаПоИдентификаторуОбъекта((string)item.МестоПоиска.id_node, хранилище, domain)
                        : Convert.ToDecimal(item.МестоПоиска.id_node);

                    if (_UnionNode == 0) continue;

                    var name = QueryColumn.ParseName(item.Атрибут);
                    //два параметра потому что объединие может быть в поиске и при выводе
                    command.Parameters.AddWithValue("@" + name + "1", _UnionNode).SqlDbType = SqlDbType.Decimal;
                    command.Parameters.AddWithValue("@" + name + "2", _UnionNode).SqlDbType = SqlDbType.Decimal;
                }
            }
            #endregion

            command.CommandText = _cache.Sql;

            return command;
        }

        /// <summary>
        /// Sql запрос из кешированных данных 
        /// </summary>
        public SqlCommand ParseTable(Query запрос)
        {
            var tablePrefix = DataClient.GetTablePrefix(хранилище);
            var command = (db.Connection as SqlConnection).CreateCommand();
            var isPagger = !(запрос.КоличествоВыводимыхДанных == 0 || запрос.КоличествоВыводимыхДанных == int.MaxValue);

            this.Колонки = new Dictionary<string, QueryColumn>();
            this.Параметры = new Dictionary<string, MemberTypes?>();

            var oRow = 0;
            var sb = new StringBuilder(5000);
            //sb.AppendLine("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            //sb.AppendLine("BEGIN TRANSACTION;");

            sb.AppendLine("SET NOCOUNT ON");
            sb.AppendLine("---------------------------------");

            #region Подготовка данных
            if (запрос.ВыводимыеКолонки != null)
            {
                foreach (var item in запрос.ВыводимыеКолонки.Where(p => p.Функция == Query.ФункцияАгрегации.Sql))
                {
                    item.Атрибут = запрос.ParseSql(item.Атрибут, domain);
                }
            }

            ResolveTypes(запрос.Типы.Count > 0 ? запрос.Типы[0].TrimEnd(new char[] { '%' }) : "object", запрос.ВыводимыеКолонки, domain);
            if (запрос.УсловияПоиска.Count > 0) ResolveAttributes(запрос.УсловияПоиска.Select(p => new Query.Колонка() { Атрибут = p.Атрибут }), domain);
            if (запрос.Сортировки.Count > 0) ResolveAttributes(запрос.Сортировки.Select(p => new Query.Колонка() { Атрибут = p.Атрибут }), domain);
            if (запрос.Группировки.Count > 0) ResolveAttributes(запрос.Группировки.Select(p => new Query.Колонка() { Атрибут = p }), domain);
            if (запрос.Объединения != null)
            {
                foreach (var item in запрос.Объединения)
                {
                    var _UnionNode = item.МестоПоиска.id_node is string
                        ? data.ПоискРазделаПоИдентификаторуОбъекта((string)item.МестоПоиска.id_node, хранилище, domain)
                        : Convert.ToDecimal(item.МестоПоиска.id_node);

                    if (!Колонки.ContainsKey(item.Атрибут))
                    {
                        Колонки.Add(item.Атрибут, new QueryColumn()
                        {
                            OriginalName = item.Атрибут,
                            Name = QueryColumn.ParseName(item.Атрибут),
                            Тип = configuration.ПолучитьТип(item.Атрибут, domain),
                            UnionNode = _UnionNode,
                            IsUnion = true
                        });
                    }
                    else
                    {
                        var c = Колонки[item.Атрибут];
                        c.UnionNode = _UnionNode;
                        c.IsUnion = true;
                    }
                }
            }

            var aggrigate_column = Enumerable.Empty<Query.Колонка>();
            if (запрос.ВыводимыеКолонки != null)
            {
                aggrigate_column = запрос.ВыводимыеКолонки.Where(p => p.Функция != Query.ФункцияАгрегации.None && p.Функция != Query.ФункцияАгрегации.Sql);
                foreach (var item in aggrigate_column)
                {
                    if (!Колонки.ContainsKey(item.Атрибут)) continue;
                    Колонки[item.Атрибут].Функция = item.Функция;
                }
            }
            if (запрос.МестаПоиска == null || запрос.МестаПоиска.Count == 0)
            {
                запрос.МестаПоиска = new List<Query.МестоПоиска>();
                запрос.МестаПоиска.Add(new Query.МестоПоиска()
                {
                    id_node = 0,
                    МаксимальнаяГлубина = 0
                });
            }

            var IsAggregate = aggrigate_column.Count() > 0 || запрос.Группировки.Count > 0;
            if (IsAggregate) isPagger = false;
            #endregion

            #region РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ
            sb.Append(@"
--РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ
declare @TotalPage  decimal(18,0) = @CurrentPage * @PageSize;
--END РАСЧЕТ ОТОБРАЖЕНИЯ КОЛ_ВА НА СТРАНИЦЕ


");
            #endregion

            if (isPagger) 
                sb.AppendLine("select * from (");

            if (запрос.КоличествоВыводимыхСтраниц == 0 || запрос.КоличествоВыводимыхСтраниц == int.MaxValue)
            {
                sb.AppendLine("select ");
            }
            else
            {
                sb.AppendLine("select top(cast(@CountSize as int))");
            }

            if (!IsAggregate)
            {
                if (isPagger)
                {
                    #region order by
                    sb.Append("\tROW_NUMBER() OVER(order by ");
                    if (запрос.Сортировки.Count == 0)
                    {
                        sb.Append("[id_node] asc");
                    }
                    else
                    {
                        oRow = 0;
                        foreach (var column in запрос.Сортировки.Distinct(new ComparerSortColumn()))
                        {
                            #region Sql
                            if (column.Направление == Query.НаправлениеСортировки.Sql)
                            {
                                if (oRow++ > 0) sb.Append(",");
                                sb.Append(column.Атрибут);
                            }
                            #endregion
                            #region else
                            else
                            {
                                if (!Колонки.ContainsKey(column.Атрибут)) continue;
                                var item = Колонки[column.Атрибут];
                                if (item.Тип == null) continue;

                                if (oRow++ > 0) sb.Append(",");
                                switch (column.Направление)
                                {
                                    case Query.НаправлениеСортировки.Rand:
                                        sb.Append("NEWID()");
                                        break;

                                    default:
                                        switch (item.Тип.MemberType)
                                        {
                                            case MemberTypes.Ссылка:
                                                sb.AppendFormat("isnull([{0}.НазваниеОбъекта],'') {1}", column.Атрибут, column.Направление.ToString());
                                                break;
                                            default:
                                                sb.AppendFormat(/*ResolveConverter(item, true) +*/ "[{0}] {1}", column.Атрибут, column.Направление.ToString());
                                                break;
                                        }
                                        break;
                                }
                            }
                            #endregion
                        }
                    }
                    sb.AppendLine(") 'НомерСтроки'");
                    #endregion

                    #region select
                    sb.AppendLine("\t,*");
                    #endregion
                }
                else
                {
                    sb.AppendLine("*");
                }
            }
            else
            {
                #region Вывод с группировками
                oRow = 0;
                foreach (var g in запрос.Группировки)
                {
                    var item = Колонки[g];
                    if(item == null) continue;

                    if (oRow++ > 0) sb.Append(",");
                    sb.AppendFormat("\n\t[{0}] 'GROUP{1}'", item.Name, oRow);
                }

                foreach (var column in aggrigate_column.Select(p => p.Атрибут).Union(запрос.ВыводимыеКолонки.Select(p => p.Атрибут)))
                {
                    var item = Колонки[column];
                    switch (item.Функция)
                    {
                        case Query.ФункцияАгрегации.None:
                            {
                                //если в запросе нет атрибута то не выводить его
                                if (запрос.Группировки != null && !запрос.Группировки.Contains(item.OriginalName))
                                    continue;

                                if (item.Тип.RegisterType == RegisterTypes.undefined) continue;
                                switch (item.Тип.MemberType)
                                {
                                    case MemberTypes.Ссылка:
                                        if (oRow++ > 0) sb.Append(",");
                                        sb.AppendFormat("\n\tisnull([{0}], 0) '{1}',", item.Name, item.OriginalName);
                                        sb.AppendFormat("\n\tisnull([{0}.НазваниеОбъекта], '') '{1}.НазваниеОбъекта'", item.Name, item.OriginalName);
                                        break;

                                    default:
                                        if (oRow++ > 0) sb.Append(",");
                                        sb.AppendFormat("\n\t" + ResolveConverter(item, false) + " '{1}'", item.Name, item.OriginalName);
                                        break;
                                }
                            }
                            break;

                        case Query.ФункцияАгрегации.Sql:
                        case Query.ФункцияАгрегации.Функция:
                            if (oRow++ > 0) sb.Append(",");
                            sb.AppendFormat("\n\t[{0}]", item.OriginalName);
                            break;

                        case Query.ФункцияАгрегации.Distinct:
                            if (oRow++ > 0) sb.Append(",");
                            sb.AppendFormat("\n\t{2}([{0}]) '{1}'", item.Name, item.OriginalName, item.Функция.ToString());
                            break;

                        default:
                            if (oRow++ > 0) sb.Append(",");
                            sb.AppendFormat("\n\tisnull({2}([{0}]), 0) '{1}'", item.Name, item.OriginalName, item.Функция.ToString());
                            break;
                    }
                    sb.AppendLine();
                }
                #endregion
            }

            sb.AppendLine("from");
            sb.AppendLine("\t" + "cache_" + QueryColumn.ParseName(запрос.CacheName) + " WITH(NOLOCK)");

            #region where
            #region МестаПоиска
            var placeBuilder = new StringBuilder();
            if (запрос.МестаПоиска != null && запрос.МестаПоиска.Count > 0)
            {
                oRow = 0;
                foreach (var item in запрос.МестаПоиска)
                {
                    #region ResolveIdNode
                    if (item != null && item.id_node != null && item.id_node is string)
                    {
                        //заменить параметр
                        if (((string)item.id_node).StartsWith("@"))
                            item.id_node = запрос.Параметры.FirstOrDefault(p => p.Имя == (string)item.id_node).Значение;

                        item.id_node = ResolveIdNode(item.id_node, хранилище, domain, false);
                    }
                    var id_node_ = Convert.ToDecimal(item.id_node);
                    #endregion

                    var param = "@HashCode" + (++oRow).ToString();

                    #region искать вверх
                    if (item.МаксимальнаяГлубина == -2)
                    {
                        //sb.AppendFormat("select [id_parent] 'id_node', [type] from {0}tblNode where id_node = {1} and id_parent > 0", tablePrefix, param);
                        //if (Типы.Length == 1) sb.AppendFormat(" and [type] = {0}", Типы[0]);
                        //else if (Типы.Length > 1) sb.AppendFormat(" and [type] in ({0})", string.Join(",", Типы));

                        //sb.AppendLine();
                        //sb.AppendLine("union all");
                        //sb.AppendFormat("select N.id_parent 'id_node', N.[type] from {0}tblNode N inner join nodes on N.id_node = nodes.id_node where N.id_parent > 0", tablePrefix);

                        //if (Типы.Length == 1) sb.AppendFormat(" and N.[type] = {0}", Типы[0]);
                        //else if (Типы.Length > 1) sb.AppendFormat(" and N.[type] in ({0})", string.Join(",", Типы));

                        //sb.AppendLine();

                        //command.Parameters.AddWithValue(param, id_node_);
                    }
                    #endregion
                    #region искать вниз
                    else
                    {
                        if (id_node_ == 0 && item.МаксимальнаяГлубина == 0)
                            continue;

                        if (oRow > 1) placeBuilder.Append(" and ");
                        placeBuilder.Append("(");

                        //найти много разделов
                        if (item.МаксимальнаяГлубина == 1)
                        {
                            placeBuilder.Append("[@РодительскийРаздел] = " + param);
                            command.Parameters.AddWithValue(param, id_node_).SqlDbType = SqlDbType.Decimal;
                        }
                        else
                        {
                            placeBuilder.Append("[HashCode] like " + param);

                            var _hash = ConfigurationClient.GetHashCode(id_node_, item.МаксимальнаяГлубина, хранилище, domain);
                            command.Parameters.AddWithValue(param, string.IsNullOrEmpty(_hash) ? "%" : _hash).SqlDbType = SqlDbType.VarChar;
                        }
                        placeBuilder.Append(")");
                    }
                    #endregion
                }
            }
            #endregion

            var whereBuilder = new StringBuilder();
            if (запрос.УсловияПоиска != null && запрос.УсловияПоиска.Count > 0)
            {
                oRow = 0;
                foreach (var item in запрос.УсловияПоиска.Where(p => !string.IsNullOrEmpty(p.Атрибут)))
                {
                    //если при полном поиске значение не задано, упращаем запрос
                    if (item.Атрибут == "*" && !запрос.IsПоискПоСодержимому) 
                        continue;


                    if (oRow > 0) 
                        whereBuilder.AppendLine(@" and ");
                    whereBuilder.Append("\t(");

                    var param = string.Format("@p{0}", ++oRow);
                    var MemberType = (MemberTypes?)null;
                    var defaultValue = item.Значение;

                    #region item.Атрибут == "*"
                    if (item.Атрибут == "*")
                    {
                        var oColumn = 0;
                        foreach (var column in Колонки.Values)
                        {
                            if (column.Тип == null) continue;

                            if (!(column.Тип.MemberType == MemberTypes.String ||
                                  column.Тип.MemberType == MemberTypes.Ссылка ||
                                  column.Тип.MemberType == MemberTypes.Int ||
                                  column.Тип.MemberType == MemberTypes.Double)) continue;
                            if (oColumn++ > 0) whereBuilder.Append(" or \n\t\t");

                            switch (column.Тип.MemberType)
                            {
                                case MemberTypes.String:
                                case MemberTypes.Double:
                                case MemberTypes.Int:
                                    whereBuilder.AppendFormat(@"[{0}] like {1}", column.OriginalName, param);
                                    //whereBuilder.AppendFormat(@"[{0}] like {1}", column.Name, param);
                                    break;

                                case MemberTypes.Ссылка:
                                    whereBuilder.AppendFormat(@"[{0}.НазваниеОбъекта] like {1}", column.OriginalName, param);
                                    //whereBuilder.AppendFormat(@"[{0}.НазваниеОбъекта] like {1}", column.Name, param);
                                    break;
                            }
                        }
                    }
                    #endregion
                    #region item.Атрибут == "id_node"
                    else if (item.Атрибут == "id_node")
                    {
                        switch (item.Оператор)
                        {
                            case Query.Оператор.Функция:
                                whereBuilder.AppendFormat(@"[id_node] {0}", item.Значение);
                                break;

                            default:
                                if (defaultValue == null) defaultValue = 0m;
                                whereBuilder.AppendFormat(@"[id_node] {1} {0}", ResolveWhereValue(MemberTypes.Double, item, запрос.ФорматДат, param, defaultValue), ResolveOperator(item.Оператор, defaultValue));
                                break;
                        }
                    }
                    #endregion
                    #region item.Оператор = Sql
                    else if (item.Оператор == Query.Оператор.Sql && string.IsNullOrEmpty(item.Атрибут))
                    {
                        whereBuilder.Append(item.Значение);
                    }
                    #endregion
                    #region else
                    else
                    {
                        if (!Колонки.ContainsKey(item.Атрибут)) continue;

                        var id_attr = Колонки[item.Атрибут];
                        if (id_attr == null || id_attr.Тип == null /*|| id_attr.Тип.RegisterType == RegisterTypes.undefined*/) continue;

                        MemberType = id_attr.Тип.MemberType;
                        defaultValue = DefaultValue(MemberType, item.Оператор, item.Значение);

                        switch (item.Оператор)
                        {
                            case Query.Оператор.Соодержит:
                            case Query.Оператор.СоодержитСлева:
                            case Query.Оператор.СоодержитСправа:
                                if (id_attr.Тип.MemberType == MemberTypes.Ссылка)
                                {
                                    if (item.Значение is string)
                                        whereBuilder.AppendFormat(@"[{0}.НазваниеОбъекта] like {1}", id_attr.OriginalName, param);
                                    else
                                        whereBuilder.AppendFormat(@"({1} <> 0 and [{0}] = {1}) or {1} = 0", id_attr.OriginalName, param);
                                }
                                else
                                {
                                    whereBuilder.AppendFormat(@"{0} like {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, true), ResolveWhereValue(id_attr.Тип.MemberType, item, запрос.ФорматДат, param, defaultValue));
                                }
                                break;

                            case Query.Оператор.Функция:
                                whereBuilder.AppendFormat(@"{0} {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, true), item.Значение);
                                break;

                            case Query.Оператор.Sql:
                                whereBuilder.AppendFormat(Convert.ToString(item.Значение), ResolveWhereColumn(id_attr, item, запрос.ФорматДат, true));
                                break;

                            default:
                                whereBuilder.AppendFormat(@"{0} {2} {1}", ResolveWhereColumn(id_attr, item, запрос.ФорматДат, true), ResolveWhereValue(id_attr.Тип.MemberType, item, запрос.ФорматДат, param, defaultValue), ResolveOperator(item.Оператор, defaultValue));
                                break;
                        }
                    }
                    #endregion

                    whereBuilder.Append(@")");

                    //добавить значение, если значение не определено заменить на значение по-умолчанию
                    if (!command.Parameters.Contains(param))
                    {
                        AddWithValue(command, param, defaultValue ?? Convert.DBNull);
                    }
                    Параметры[param] = MemberType ?? MemberTypes.String;
                }
            }

            if (placeBuilder.Length > 0 || whereBuilder.Length > 0)
            {
                sb.AppendLine("where");
                if (placeBuilder.Length > 0)
                {
                    sb.AppendLine(placeBuilder.ToString());
                }

                if (whereBuilder.Length > 0)
                {
                    if (placeBuilder.Length > 0)
                        sb.AppendLine(" and");
                    sb.Append(whereBuilder.ToString());
                }
            }
            #endregion

            #region group by
            if (запрос.Группировки != null && запрос.Группировки.Count > 0)
            {
                sb.AppendLine();
                sb.Append(@"group by");
                oRow = 0;
                foreach (var item in запрос.Группировки)
                {
                    if (oRow++ > 0) sb.Append(",");

                    var column = Колонки[item];
                    switch (column.Тип.MemberType)
                    {
                        case MemberTypes.Ссылка:
                            sb.AppendFormat("\n\t[{0}], [{0}.НазваниеОбъекта]", column.Name);
                            break;
                        default:
                            sb.AppendFormat("\n\t[{0}]", column.Name);
                            break;
                    }
                }
                sb.AppendLine();
            }
            #endregion

            #region params
            if (запрос.Параметры != null)
            {
                foreach (var item in запрос.Параметры)
                {
                    if (command.Parameters.Contains(item.Имя)) continue;
                    AddWithValue(command, item.Имя, item.Значение ?? string.Empty);
                }
            }
            #endregion

            if (isPagger)
            {
                sb.AppendLine();
                sb.AppendLine(") as T where [НомерСтроки] > @TotalPage and [НомерСтроки] <= @TotalPage + @PageSize");

                #region total
                sb.AppendLine("\n\n");
                sb.AppendLine("select top 1 count(*) as TotalRows, case when @PageSize = 0 then 0 else ceiling(cast(count(*) as decimal(18,0)) / @PageSize) end as PageCount");
                sb.AppendLine("from cache_" + QueryColumn.ParseName(запрос.CacheName) + " WITH(NOLOCK)");

                if (placeBuilder.Length > 0 || whereBuilder.Length > 0)
                {
                    sb.AppendLine("where");
                    if (placeBuilder.Length > 0)
                    {
                        sb.AppendLine(placeBuilder.ToString());
                    }

                    if (whereBuilder.Length > 0)
                    {
                        if (placeBuilder.Length > 0)
                            sb.AppendLine(" and");
                        sb.Append(whereBuilder.ToString());
                    }
                }
                #endregion
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("order by");
                if (запрос.Сортировки.Count == 0 && !IsAggregate)
                {
                    sb.Append("[id_node] asc");
                }
                else
                {
                    oRow = 0;
                    foreach (var column in запрос.Сортировки.Distinct(new ComparerSortColumn()))
                    {
                        #region Sql
                        if (column.Направление == Query.НаправлениеСортировки.Sql)
                        {
                            if (oRow++ > 0) sb.Append(",");
                            sb.Append(column.Атрибут);
                        }
                        #endregion
                        #region else
                        else
                        {
                            if (!Колонки.ContainsKey(column.Атрибут)) continue;
                            var item = Колонки[column.Атрибут];
                            if (item.Тип == null) continue;

                            if (oRow++ > 0) sb.Append(",");
                            switch (column.Направление)
                            {
                                case Query.НаправлениеСортировки.Rand:
                                    sb.Append("NEWID()");
                                    break;

                                default:
                                    switch (item.Тип.MemberType)
                                    {
                                        case MemberTypes.Ссылка:
                                            sb.AppendFormat("isnull([{0}.НазваниеОбъекта],'') {1}", column.Атрибут, column.Направление.ToString());
                                            break;
                                        default:
                                            sb.AppendFormat(/*ResolveConverter(item, true) +*/ "[{0}] {1}", column.Атрибут, column.Направление.ToString());
                                            break;
                                    }
                                    break;
                            }
                        }
                        #endregion
                    }
                }
            }

            sb.AppendLine();
            //sb.AppendLine("COMMIT TRANSACTION;");

            command.CommandText = sb.ToString();
            return command;
        }

        #region Resolve
        private class IEqualityComparerКолонка : IEqualityComparer<Query.Колонка>
        {
            public bool Equals(Query.Колонка x, Query.Колонка y)
            {
                return string.Equals(x.Атрибут, y.Атрибут);
            }
            public int GetHashCode(Query.Колонка obj)
            {
                return obj.Атрибут.GetHashCode();
            }
        }

        private class ComparerSortColumn : IEqualityComparer<Query.Сортировка>
        {
            public bool Equals(Query.Сортировка x, Query.Сортировка y)
            {
                return string.Equals(x.Атрибут, y.Атрибут);
            }
            public int GetHashCode(Query.Сортировка obj)
            {
                return obj.Атрибут.GetHashCode();
            }
        }
        private void ResolveTypes(string ТипДанных, IEnumerable<RosService.Data.Query.Колонка> attributes, string domain)
        {
            if (attributes != null && attributes.Count() > 0)
            {
                if (attributes.FirstOrDefault(p => p.Атрибут == "*") != null)
                {
                    foreach (var item in configuration.СписокАтрибутов(ТипДанных, domain)
                        .Where(p => p.MemberType != MemberTypes.Object)) //p.MemberType != MemberTypes.Таблица &&
                    {
                        if (!(item.ReflectedType == ТипДанных || item.Name == "НазваниеОбъекта" ||
                            item.Name == "ИдентификаторОбъекта" || item.Name == "СсылкаНаОбъект")) continue;
                        Колонки.Add(item.Name, new QueryColumn(item));
                    }
                }
                else if (attributes.FirstOrDefault(p => p.Атрибут == "**") != null)
                {
                    foreach (var item in configuration.СписокАтрибутов(ТипДанных, domain)
                        .Where(p => p.MemberType != MemberTypes.Object)) //p.MemberType != MemberTypes.Таблица && 
                    {
                        if (!(item.ReflectedType != "object" || item.Name == "НазваниеОбъекта" ||
                            item.Name == "ИдентификаторОбъекта" || item.Name == "СсылкаНаОбъект")) continue;
                        Колонки.Add(item.Name, new QueryColumn(item));
                    }
                }
                ResolveAttributes(attributes, domain);
            }
        }
        internal void ResolveAttributes(IEnumerable<Query.Колонка> attributes, string domain)
        {
            foreach (var item in attributes.ToArray())
            {
                if (Колонки.ContainsKey(item.Атрибут) || item.Атрибут == "**" || item.Атрибут == "*")
                {
                    continue;
                }
                else if (item.Функция == Query.ФункцияАгрегации.Sql || item.Функция == Query.ФункцияАгрегации.Функция)
                {
                    Колонки[item.Атрибут] = new QueryColumn() { Функция = Query.ФункцияАгрегации.Sql, OriginalName = item.Атрибут, Name = item.Атрибут };
                }
                else if (item.Атрибут.Contains('/'))
                {
                    var path = item.Атрибут.Split('/');
                    var last = null as string;
                    for (int i = 0; i < path.Length; i++)
                    {
                        var __IsJoin = i < path.Length - 1;
                        var names = path.Take(i + 1).ToArray();
                        var c = string.Join("/", names);
                        if (!Колонки.ContainsKey(c))
                        {
                            var currentPathItem = path.ElementAt(i);
                            var fullName = string.Join("_", names.Select(a => QueryColumn.ParseName(a)));
                            var __type = configuration.ПолучитьТип(currentPathItem, domain);
                            if (__type == null) throw new Exception(string.Format("Не определен тип '{0}' для атрибута '{1}'", currentPathItem, item.Атрибут));
                            Колонки[c] = new QueryColumn()
                            {
                                OriginalName = c,
                                Name = fullName,
                                Тип = __type,
                                Path = last,
                                IsPath = i > 0,
                                IsJoin = __IsJoin
                            };
                            last = fullName;
                        }
                        else if (__IsJoin)
                        {
                            Колонки[c].IsJoin = __IsJoin;
                            last = Колонки[c].Name;
                        }
                    }
                }
                else
                {
                    var type = configuration.ПолучитьТип(item.Атрибут, domain);
                    if (type == null)
                    {
                        type = new Configuration.Type()
                        {
                            Name = item.Атрибут,
                            Описание = item.Атрибут,
                            RegisterType = RegisterTypes.string_value,
                            MemberType = MemberTypes.String,
                            BaseType = "string",
                        };
                        //continue;
                    }
                    Колонки[type.Name] = new QueryColumn(type) { ТипВывода = item.Тип, ПолнотекстовыйВывод = item.ПолнотекстовыйВывод };
                }
            };
        }
        private string ResolveDateFormat(Query.ФорматДаты format)
        {
            switch (format)
            {
                case Query.ФорматДаты.ПоУмолчанию:
                    return @"{0}";

                case Query.ФорматДаты.День:
                    return @"cast({0} as date)";

                case Query.ФорматДаты.Неделя:
                    throw new Exception("Формат даты неделя не реализован");

                case Query.ФорматДаты.Месяц:
                    return @"convert(varchar(7),{0},102)";

                case Query.ФорматДаты.Квартал:
                    throw new Exception("Формат даты квартал не реализован");

                case Query.ФорматДаты.Год:
                    return @"convert(varchar(4),{0},112)";
            }

            return null;
        }
        private string ResolveWhereColumnInner(QueryColumn attr, Query.УсловиеПоиска cond, Query.ФорматДаты format)
        {
            switch (attr.Тип.MemberType)
            {
                case MemberTypes.DateTime:
                    switch (format)
                    {
                        case Query.ФорматДаты.ПоУмолчанию:
                            return cond.УчитыватьВремя ? @"[datetime_value]" : @"cast([datetime_value] as date)";

                        default:
                            return string.Format(ResolveDateFormat(format), @"[datetime_value]");
                    }

                case MemberTypes.Bool:
                case MemberTypes.Double:
                case MemberTypes.Int:
                case MemberTypes.Ссылка:
                    return @"isnull([double_value], 0)";

                case MemberTypes.Byte:
                    return @"[byte_value]";

                case MemberTypes.String:
                default:
                    return @"isnull([string_value_index], '')";
            }
        }
        private string ResolveWhereColumn(QueryColumn attr, Query.УсловиеПоиска cond, Query.ФорматДаты format, bool IsCache)
        {
            if (!IsCache)
            {
                switch (attr.Тип.MemberType)
                {
                    case MemberTypes.DateTime:
                        switch (format)
                        {
                            case Query.ФорматДаты.ПоУмолчанию:
                                return cond.УчитыватьВремя ? attr.Name + @".[value]" : string.Format(@"cast({0}.[value] as date)", attr.Name);

                            default:
                                return string.Format(ResolveDateFormat(format), attr.Name + @".[value]");
                        }

                    case MemberTypes.String:
                        return string.Format(@"isnull({0}.[value], '')", attr.Name);
                    case MemberTypes.Bool:
                    case MemberTypes.Double:
                    case MemberTypes.Int:
                    case MemberTypes.Ссылка:
                        return string.Format(@"isnull({0}.[value], 0)", attr.Name);
                    default:
                        return attr.Name + @".[value]";
                }
            }
            else
            {
                switch (attr.Тип.MemberType)
                {
                    case MemberTypes.DateTime:
                        switch (format)
                        {
                            case Query.ФорматДаты.ПоУмолчанию:
                                return cond.УчитыватьВремя ? @"[" + attr.OriginalName + @"]" : string.Format(@"cast([{0}] as date)", attr.OriginalName);

                            default:
                                return string.Format(ResolveDateFormat(format), @"[" + attr.OriginalName + @"]");
                        }

                    case MemberTypes.String:
                        return string.Format(@"isnull([{0}], '')", attr.OriginalName);
                    case MemberTypes.Bool:
                    case MemberTypes.Double:
                    case MemberTypes.Int:
                    case MemberTypes.Ссылка:
                        return string.Format(@"isnull([{0}], 0)", attr.OriginalName);
                    default:
                        return @"[" + attr.OriginalName + @"]";
                }
            }
        }
        private string ResolveWhereValue(MemberTypes memberType, Query.УсловиеПоиска cond, Query.ФорматДаты format, string param, object defaultValue)
        {
            if (defaultValue == null) return "null";
            switch (memberType)
            {
                case MemberTypes.DateTime:
                    {
                        switch (format)
                        {
                            case Query.ФорматДаты.ПоУмолчанию:
                                return cond.УчитыватьВремя ? param : string.Format("cast({0} as date)", param);

                            default:
                                return string.Format(ResolveDateFormat(format), param);
                        }
                    }

                default:
                    return param;
            }
        }
        private object ResolveOperator(Query.Оператор Оператор, object value)
        {
            switch (Оператор)
            {
                case Query.Оператор.Равно:
                    if (value == null) return "is";
                    return "=";
                case Query.Оператор.Соодержит:
                case Query.Оператор.СоодержитСлева:
                case Query.Оператор.СоодержитСправа:
                    return "like";
                case Query.Оператор.НеРавно:
                    if (value == null) return "is not";
                    return "<>";
                case Query.Оператор.Больше:
                    return ">";
                case Query.Оператор.БольшеРавно:
                    return ">=";
                case Query.Оператор.Меньше:
                    return "<";
                case Query.Оператор.МеньшеРавно:
                    return "<=";
                case Query.Оператор.Функция:
                default:
                    return "";
            }
        }
        private object ResolveConverter(QueryColumn attr, bool IsCache)
        {
            if (IsCache)
            {
                switch (attr.ТипВывода == MemberTypes.Undefined ? attr.Тип.MemberType : attr.ТипВывода)
                {
                    case MemberTypes.Double:
                        return @"isnull([{0}],0)";

                    case MemberTypes.Ссылка:
                        return @"isnull([{0}],0)";
                    //return @"isnull(convert(numeric(18,0),[{0}]),0)";

                    case MemberTypes.Int:
                        return @"isnull(convert(int, [{0}]),0)";

                    case MemberTypes.Bool:
                        return @"isnull(convert(bit, [{0}]),0)";

                    case MemberTypes.DateTime:
                        return @"[{0}]";

                    case MemberTypes.String:
                        return @"isnull([{0}],'')";

                    case MemberTypes.Object:
                        return @"isnull(convert(varchar(900), [{0}]),'')";

                    default:
                        return @"[{0}]";
                }
            }
            else
            {
                switch (attr.ТипВывода == MemberTypes.Undefined ? attr.Тип.MemberType : attr.ТипВывода)
                {
                    case MemberTypes.Double:
                    case MemberTypes.Ссылка:
                        return @"isnull({0}.[value],0)";

                    case MemberTypes.Int:
                        return @"isnull(convert(int, {0}.[value]),0)";

                    case MemberTypes.Bool:
                        return @"isnull(convert(bit, {0}.[value]),0)";

                    case MemberTypes.DateTime:
                        return @"{0}.[value]";

                    case MemberTypes.String:
                        return @"isnull({0}.[value],'')";

                    case MemberTypes.Object:
                        return @"isnull(convert(varchar(900), {0}.[value]),'')";

                    default:
                        return @"{0}.[value]";
                }
            }
        }
        internal static object DefaultValue(MemberTypes? MemberType, Query.Оператор? Оператор, object Значение)
        {
            if (Значение == null)
            {
                switch (MemberType)
                {
                    case MemberTypes.String:
                        Значение = string.Empty;
                        break;

                    case MemberTypes.Ссылка:
                    case MemberTypes.Int:
                    case MemberTypes.Double:
                        Значение = 0m;
                        break;

                    case MemberTypes.Bool:
                        Значение = false;
                        break;

                    case MemberTypes.DateTime:
                        {
                            switch (Оператор)
                            {
                                case Query.Оператор.Больше:
                                case Query.Оператор.БольшеРавно:
                                    Значение = System.Data.SqlTypes.SqlDateTime.MinValue.Value;
                                    break;

                                case Query.Оператор.Меньше:
                                case Query.Оператор.МеньшеРавно:
                                    Значение = System.Data.SqlTypes.SqlDateTime.MaxValue.Value;
                                    break;

                                //default:
                                //    Значение = Значение ?? Convert.DBNull;
                                //    break;
                            }
                        }
                        break;
                }
            }

            switch (Оператор)
            {
                case Query.Оператор.Соодержит:
                    {
                        //для ссылок, при не строковом поиске не добавлять
                        if (MemberType == MemberTypes.Ссылка && !(Значение is string))
                            return Значение;

                        var str = Convert.ToString(Значение);
                        if (string.IsNullOrEmpty(str) || !(str.StartsWith("%") | str.EndsWith("%")))
                            Значение = "%" + Значение + "%";
                    }
                    break;

                case Query.Оператор.СоодержитСлева:
                    {
                        //для ссылок, при не строковом поиске не добавлять
                        if (MemberType == MemberTypes.Ссылка && !(Значение is string))
                            return Значение;

                        var str = Convert.ToString(Значение);
                        if (string.IsNullOrEmpty(str) || !str.EndsWith("%"))
                            Значение = Значение + "%";
                    }
                    break;

                case Query.Оператор.СоодержитСправа:
                    {
                        //для ссылок, при не строковом поиске не добавлять
                        if (MemberType == MemberTypes.Ссылка && !(Значение is string))
                            return Значение;

                        var str = Convert.ToString(Значение);
                        if (string.IsNullOrEmpty(str) || !str.StartsWith("%"))
                            Значение = "%" + Значение;
                    }
                    break;
            }

            return Значение;
        }
        internal static object DefaultValue(MemberTypes MemberType)
        {
            switch (MemberType)
            {
                case MemberTypes.Bool:
                    return false;

                case MemberTypes.String:
                    return string.Empty;

                case MemberTypes.Int:
                case MemberTypes.Double:
                case MemberTypes.Ссылка:
                    return 0;

                case MemberTypes.Byte:
                case MemberTypes.DateTime:
                default:
                    return Convert.DBNull;
            }
        }


        internal static decimal ResolveIdNode(object node, Хранилище хранилище, string domain, bool IsTry)
        {
            if (node == null)
            {
                return 0m;
            }
            else if (node is decimal)
            {
                return (decimal)node;
            }
            else if (node is string)
            {
                var guid = ((string)node);
                var __cachePathNode = Cache.KeyResolve(domain, guid);
                var cacheItem = Cache.GetResolve(__cachePathNode);
                if (cacheItem != null)
                {
                    return cacheItem.id_node;
                }
                else if (guid.StartsWith("&"))
                {
                    if (guid.Length == 37)
                    {
                        //поиск по GuidCode
                        return new Data.DataClient().ПоискРазделаПоКлючу(guid.Substring(1), хранилище, domain, IsTry);
                    }
                    else
                    {
                        //поиск по ИдентификаторуОбъекта
                        //&ЛогинПользователя=Техподдержка
                        return new Data.DataClient().ПоискРазделаПоИдентификаторуОбъекта(guid, хранилище, domain, IsTry);
                    }
                }
                else if (guid.IsGuid())
                {
                    return new Data.DataClient().ПоискРазделаПоКлючу(guid, хранилище, domain, IsTry);
                }
                else
                {
                    var id_node = 0m;
                    if (decimal.TryParse(guid, out id_node))
                    {
                        //проверить на ввод числа
                        Cache.SetResolve(__cachePathNode, new RosService.Caching.КешИдентификаторРаздела()
                        {
                            id_node = id_node
                        });
                        return id_node;
                    }
                    else
                    {
                        //поиск по ИдентификаторуОбъекта
                        return new Data.DataClient().ПоискРазделаПоИдентификаторуОбъекта(guid, хранилище, domain, IsTry);
                    }
                }
            }
            return Convert.ToDecimal(node);
        }
        internal static IEnumerable<string> ResolveAttribute(string ТипДанных, string attribute, string domain)
        {
            var items = new List<string>();
            if (attribute == "*" || attribute == "**")
            {
                items.AddRange(new ConfigurationClient().СписокАтрибутов(ТипДанных, domain).Select(p => p.Name));
            }
            //if (attribute == "*")
            //{
            //    items.AddRange(new ConfigurationClient().СписокАтрибутов(ТипДанных, domain).Where(p =>
            //        p.ReflectedType == ТипДанных).Select(p => p.Name));
            //    items.AddRange(new string[] { "НазваниеОбъекта", "ИдентификаторОбъекта" });
            //}
            //else if (attribute == "**")
            //{
            //    items.AddRange(new ConfigurationClient().СписокАтрибутов(ТипДанных, domain).Where(p =>
            //            p.ReflectedType != "object").Select(p => p.Name));
            //    items.AddRange(new string[] { "НазваниеОбъекта", "ИдентификаторОбъекта" });
            //}
            return items.ToArray();
        }

        //internal static void AddWithValue(SqlCommand command, string name, object value, MemberTypes? type)
        //{
        //    if (type == null)
        //    {
        //        AddWithValue(command, name, value);
        //    }
        //    else
        //    {
        //        switch (type)
        //        {
        //            case MemberTypes.String:
        //                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.VarChar;
        //                break;
        //            case MemberTypes.Int:
        //            case MemberTypes.Ссылка:
        //            case MemberTypes.Double:
        //                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.Decimal;
        //                break;
        //            case MemberTypes.DateTime:
        //                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.DateTime;
        //                break;
        //            case MemberTypes.Bool:
        //                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.Bit;
        //                break;
        //            case MemberTypes.Byte:
        //                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.VarBinary;
        //                break;
        //            default:
        //                command.Parameters.AddWithValue(name, value);
        //                break;
        //        }
        //    }
        //}
        internal static void AddWithValue(SqlCommand command, string name, object value)
        {
            if (value is string)
            {
                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.VarChar;
                //var len = ((string)value).Length;
                //var param = command.Parameters.AddWithValue(name, value);
                //param.SqlDbType = SqlDbType.VarChar;
                //if (len > 900)
                //{
                //}
                //else if (len > 512)
                //    param.Size = 900;
                //else if (len > 128)
                //    param.Size = 512;
                //else if (len > 50)
                //    param.Size = 128;
                //else
                //    param.Size = 50;
            }

            else if (value is DateTime)
            {
                if (DateTime.MinValue.Equals(value))
                    value = System.Data.SqlTypes.SqlDateTime.MinValue;
                if (DateTime.MaxValue.Equals(value))
                    value = System.Data.SqlTypes.SqlDateTime.MaxValue;

                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.DateTime;
            }

            else if (value is decimal || value is int || value is double || value is float)
                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.Decimal;

            else if (value is bool)
                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.Bit;

            else if (value is byte[])
                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.VarBinary;

            else
                command.Parameters.AddWithValue(name, value);
        }
        #endregion

        #region Create sql table
        internal static void CreateTable(SqlConnection connection, string tableName, Dictionary<string, QueryColumn> columns)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            var sql = new StringBuilder();
            sql.AppendFormat("IF OBJECT_ID('{0}') IS NOT NULL\nDROP TABLE {0};", tableName);
            sql.AppendLine();
            sql.AppendLine();

            sql.AppendFormat("CREATE TABLE [{0}] (", tableName);
            sql.AppendLine();
            sql.AppendLine("\t[id_node] [numeric](18, 0) NOT NULL,");
            sql.AppendLine("\t[type] [nvarchar](128) NOT NULL,");
            sql.AppendLine("\t[GuidCode] [varchar](900) NOT NULL,");
            sql.Append("\t[HashCode] [varchar](900) NOT NULL");

            foreach (var p in columns.Values)
            {
                if (p.Тип == null)
                    continue;
                if (p.Name == "GuidCode" || p.Name == "HashCode")
                    continue;

                sql.Append(",");
                sql.AppendLine();

                switch (p.Тип.MemberType)
                {
                    case MemberTypes.String:
                        {
                            var size = p.ПолнотекстовыйВывод.GetValueOrDefault() ? "max" : "512";
                            sql.AppendFormat("\t[{0}] [varchar]({1})", p.OriginalName, size);
                        }
                        break;

                    case MemberTypes.Int:
                        sql.AppendFormat("\t[{0}] [numeric](18, 0)", p.OriginalName);
                        break;

                    case MemberTypes.Double:
                        sql.AppendFormat("\t[{0}] [numeric](28, 13)", p.OriginalName);
                        break;

                    case MemberTypes.DateTime:
                        sql.AppendFormat("\t[{0}] [datetime]", p.OriginalName);
                        break;

                    case MemberTypes.Bool:
                        sql.AppendFormat("\t[{0}] [bit]", p.OriginalName);
                        break;

                    case MemberTypes.Ссылка:
                        sql.AppendFormat("\t[{0}] [numeric](18, 0),", p.OriginalName);
                        sql.AppendLine();
                        sql.AppendFormat("\t[{0}.НазваниеОбъекта] [varchar](512)", p.OriginalName);
                        break;
                }
            }
            sql.AppendLine();
            sql.AppendLine(")");

            var command = connection.CreateCommand();
            command.CommandText = sql.ToString();
            command.ExecuteNonQuery();
        }
        #endregion
    }

    internal class QueryColumn
    {
        public string Name;
        public string OriginalName;
        public bool IsPath;
        public bool IsHide;

        public RosService.Data.Query.ФункцияАгрегации Функция;
        public RosService.Configuration.Type Тип;
        public MemberTypes ТипВывода;
        public string Path;
        public bool IsJoin;
        public bool IsUnion;
        public decimal UnionNode;
        public bool? ПолнотекстовыйВывод;

        public QueryColumn()
        {
            IsHide = false;
        }
        public QueryColumn(RosService.Configuration.Type type)
        {
            IsHide = false;

            if (type != null)
            {
                OriginalName = type.Name;
                Name = ParseName(type.Name);
                Тип = type;
            }
        }
        public static string ParseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return Regex.Replace(name.Replace('.', '_').Replace(':', '_').Replace('-', '_'), @"[^\w]", "");
        }
        public static string ParseAttrInsert(string name)
        {
            return name.Replace("]", "]]");
        }
    };
}
