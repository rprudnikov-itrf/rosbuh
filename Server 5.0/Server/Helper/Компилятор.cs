using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Transactions;
using System.Data;
using RosService.Intreface;
using RosService.Data;
using RosService.Configuration;
using RosService.Caching;

namespace RosService.Compile
{
    internal static class Компилятор
    {
        private static RegisterTypes ТипРегистра(string ТипДанных)
        {
            switch (ТипДанных)
            {
                case "string":
                    return RegisterTypes.string_value;

                case "bool":
                case "double":
                case "int":
                case "ссылка":
                    return RegisterTypes.double_value;

                case "datetime":
                    return RegisterTypes.datetime_value;

                case "byte":
                    return RegisterTypes.byte_value;

                case "object":
                default:
                    return RegisterTypes.undefined;
            }
        }
        private static MemberTypes ТипЗанчения(string ТипДанных)
        {
            switch (ТипДанных)
            {
                case "string":
                    return MemberTypes.String;
                case "bool":
                    return MemberTypes.Bool;
                case "byte":
                    return MemberTypes.Byte;
                case "datetime":
                    return MemberTypes.DateTime;
                case "double":
                    return MemberTypes.Double;
                case "int":
                    return MemberTypes.Int;
                case "ссылка":
                    return MemberTypes.Ссылка;
                case "object":
                default:
                    return MemberTypes.Object;
            }
        }
        private static string GetTypeHashCode(decimal id_node, string domain)
        {
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            {
                #region sql
                var sql = @";with
                                БазовыйТип        as (select id_node, [double_value] 'value' from assembly_tblValueHref WITH(NOLOCK) where [type] = 'БазовыйТип'),
	                            НомерТипаДаннах   as (select id_node, [double_value] 'value' from assembly_tblValueDouble WITH(NOLOCK) where [type] = 'НомерТипаДаннах'),
                                СписокТиповДанных as (select  N.id_node, БазовыйТип.value 'БазовыйТип' from assembly_tblNode N WITH(NOLOCK) left join БазовыйТип WITH(NOLOCK) on N.id_node = БазовыйТип.id_node where N.[type] = 'ТипДанных' and N.HashCode like @id_node),
	                            ВыбранныйТипДанных as (select * from СписокТиповДанных WITH(NOLOCK) where id_node = @id_type union all select N.* from СписокТиповДанных N WITH(NOLOCK) inner join ВыбранныйТипДанных on N.id_node = ВыбранныйТипДанных.БазовыйТип and N.id_node > 0)
                            select 
	                            НомерТипаДаннах.value 'id_type'
                            from
	                            ВыбранныйТипДанных WITH(NOLOCK) 
	                            left join НомерТипаДаннах WITH(NOLOCK) on НомерТипаДаннах.id_node = ВыбранныйТипДанных.id_node";
                #endregion

                using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                {
                    var command = (db.Connection as SqlConnection).CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@id_type", id_node);
                    command.Parameters.AddWithValue("@id_node", ConfigurationClient.GetHashCode(Convert.ToDecimal(ConfigurationClient.СистемныеПапки.РазделТипы), 0, Хранилище.Конфигурация, domain));
                    if (new SqlDataAdapter(command).Fill(table) > 0)
                    {
                        var hash = "";
                        foreach (var item in table.Tables[0].AsEnumerable())
                            hash = ConfigurationClient.GetHashKey(item.Field<decimal>("id_type")) + hash;
                        return hash;
                    }
                }
                return "";
            }
        }
        private static CompileType GetAssemblyAttribute(DataRow p)
        {
            try
            {
                return new CompileType()
                {
                    //id_node = p.Field<decimal>("id_node"),
                    id_type = p.Field<decimal>("id_type"),
                    Name = p.Field<string>("Name"),
                    Namespace = p.Field<string>("Namespace"),
                    Описание = p.Field<string>("Описание"),
                    DeclaringType = Convert.ToDecimal(p["DeclaringType"]),
                    BaseType = p.Field<string>("BaseType"),
                    ReflectedType = p.Field<string>("ReflectedType"),
                    IsReadOnly = Convert.ToBoolean(p["IsReadOnly"]),
                    //IsSystem = Convert.ToBoolean(p["IsSystem"]),
                    IsAutoIncrement = Convert.ToBoolean(p["IsAutoIncrement"]),
                    IsSetDefaultValue = Convert.ToBoolean(p["IsSetDefaultValue"]),
                    HashCode = ConfigurationClient.GetHashKey(p.Field<decimal>("id_type")),
                    MemberType = ТипЗанчения(p.Field<string>("BaseType")),
                    RegisterType = ТипРегистра(p.Field<string>("BaseType"))
                };
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), "", "", "КомпилироватьЗависимыеТипыДанных");
                throw ex;
            }
        }
        private static IEnumerable<decimal> СписокРазделов(string domain)
        {
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            {
                return db.ExecuteQuery<decimal>(@"select [id_node] from assembly_tblNode WITH(NOLOCK) where [type] = 'ТипДанных' and HashCode like {0}", ConfigurationClient.GetHashCode(Convert.ToDecimal(ConfigurationClient.СистемныеПапки.РазделТипы), 0, Хранилище.Конфигурация, domain)).ToArray();

                //var command = ((SqlConnection)db.Connection).CreateCommand();
                //command.CommandText = "select [id_node] from assembly_tblNode WITH(NOLOCK) where [type] = 'ТипДанных' and HashCode like {0}";
                //return Convert.ToDecimal(command.ExecuteScalar());
            }
        }
        private static IEnumerable<CompileType> СписокАтрибутов(decimal id_node, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            {
                #region sql
                var sql = string.Format(@"
                    ;with
                        БазовыйТип        as (select id_node, [double_value] 'value' from assembly_tblValueHref WITH(NOLOCK) where [type] = 'БазовыйТип'),
                        СсылкаНаТипДанных as (select id_node, [double_value] 'value' from assembly_tblValueHref WITH(NOLOCK) where [type] = 'СсылкаНаТипДанных'),
                        НомерТипаДаннах   as (select id_node, [double_value] 'value' from assembly_tblValueDouble WITH(NOLOCK) where [type] = 'НомерТипаДаннах'),
                        IsSystem          as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsSystem'),
                        IsAutoIncrement   as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsAutoIncrement'),
                        IsReadOnly	      as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsReadOnly'),
                        IsSetDefaultValue as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsSetDefaultValue'),
                        ИмяТипаДанных     as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'ИмяТипаДанных'),
                        Namespace         as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'КатегорияТипаДанных'),
                        Описание		  as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'НазваниеОбъекта'),

                        СписокТиповДанных as (select  N.id_node, БазовыйТип.value 'БазовыйТип' from assembly_tblNode N WITH(NOLOCK) left join БазовыйТип WITH(NOLOCK) on N.id_node = БазовыйТип.id_node where N.[type] = 'ТипДанных' and N.HashCode like @РазделТипыДанных),
                        СписокАтрибутов as (select N.id_node, N.id_parent, СсылкаНаТипДанных.value 'СсылкаНаТипДанных' from assembly_tblNode N WITH(NOLOCK) left join СсылкаНаТипДанных WITH(NOLOCK) on N.id_node = СсылкаНаТипДанных.id_node where N.[type] = 'Атрибут' and N.HashCode like @РазделТипыДанных),
                        ВыбранныйТипДанных as (select * from СписокТиповДанных WITH(NOLOCK) where id_node = @id_node union all select N.* from СписокТиповДанных N WITH(NOLOCK) inner join ВыбранныйТипДанных on N.id_node = ВыбранныйТипДанных.БазовыйТип and N.id_node > 0)

                    select 
                        СписокАтрибутов.СсылкаНаТипДанных 'id_node',
                        isnull(Namespace.value,'') 'Namespace',
                        isnull(Н1.value,0) 'id_type',
                        isnull(И1.value,'') 'Name',
                        isnull(О1.value,'') 'Описание',
                        isnull(И2.value,'') 'BaseType',
                        isnull(Н2.value,0)  'BaseType.id_type',
                        isnull(И3.value,'') 'ReflectedType',
                        isnull(Н0.value,0) 'DeclaringType',
                        isnull(IsSystem.value,0) 'IsSystem',
                        isnull(IsAutoIncrement.value,0) 'IsAutoIncrement',
                        isnull(IsReadOnly.value,0) 'IsReadOnly',
                        isnull(IsSetDefaultValue.value,0) 'IsSetDefaultValue'

                    from
                        ВыбранныйТипДанных WITH(NOLOCK) 
                        inner join СписокАтрибутов WITH(NOLOCK) on СписокАтрибутов.id_parent = ВыбранныйТипДанных.id_node
                        left join СписокТиповДанных WITH(NOLOCK) on СписокТиповДанных.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join Namespace WITH(NOLOCK) on Namespace.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join Описание О1 WITH(NOLOCK) on О1.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join ИмяТипаДанных И1 WITH(NOLOCK) on И1.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join ИмяТипаДанных И2 WITH(NOLOCK) on И2.id_node = СписокТиповДанных.БазовыйТип
                        left join ИмяТипаДанных И3 WITH(NOLOCK) on И3.id_node = ВыбранныйТипДанных.id_node
                        left join НомерТипаДаннах Н0 WITH(NOLOCK) on Н0.id_node = @id_node
                        left join НомерТипаДаннах Н1 WITH(NOLOCK) on Н1.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join НомерТипаДаннах Н2 WITH(NOLOCK) on Н2.id_node = СписокТиповДанных.БазовыйТип
                        left join IsSystem WITH(NOLOCK) on IsSystem.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join IsAutoIncrement WITH(NOLOCK) on IsAutoIncrement.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join IsReadOnly WITH(NOLOCK) on IsReadOnly.id_node = СписокАтрибутов.СсылкаНаТипДанных
                        left join IsSetDefaultValue WITH(NOLOCK) on IsSetDefaultValue.id_node = СписокАтрибутов.СсылкаНаТипДанных", id_node);

                var command = (db.Connection as SqlConnection).CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 120;
                command.Parameters.AddWithValue("@РазделТипыДанных", ConfigurationClient.GetHashCode(Convert.ToDecimal(ConfigurationClient.СистемныеПапки.РазделТипы), 0, Хранилище.Конфигурация, domain));
                command.Parameters.AddWithValue("@id_node", id_node);
                new SqlDataAdapter(command).Fill(table);
                #endregion
                if (table.Tables[0].AsEnumerable().Count() > 0)
                {
                    var TypeHashCode = GetTypeHashCode(id_node, domain);
                    foreach (var item in table.Tables[0].AsEnumerable())
                    {
                        var type = GetAssemblyAttribute(item);
                        type.TypeHashCode = TypeHashCode + ConfigurationClient.GetHashKey(item.Field<decimal>("BaseType.id_type"));
                        switch ((MemberTypes)type.MemberType)
                        {
                            case MemberTypes.Undefined:
                            case MemberTypes.Object:
                                {
                                    var attrs = СписокАтрибутов(item.Field<decimal>("id_node"), domain);
                                    foreach (var subitem in attrs)
                                    {
                                        //не добавлять атрибуты из уровня object класса
                                        if (string.IsNullOrEmpty(subitem.ReflectedType) || subitem.ReflectedType == "object") continue;

                                        subitem.Name = type.Name + "." + subitem.Name;
                                        subitem.HashCode = type.HashCode + subitem.HashCode;
                                        subitem.ReflectedType = type.ReflectedType;
                                        subitem.DeclaringType = type.DeclaringType;
                                        yield return subitem;

                                        var subitem2 = (CompileType)((ICloneable)subitem).Clone();
                                        subitem2.DeclaringType = 0;
                                        subitem2.ReflectedType = "";
                                        yield return subitem2;
                                    }
                                }
                                break;

                            default:
                                yield return type;
                                break;
                        }
                    }
                }
            }
        }
        private static IEnumerable<CompileType> СписокТиповДанных(decimal id_node, string domain)
        {
            //using (new TransactionScope(TransactionScopeOption.Suppress))
            using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
            using (var table = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            {
                #region sql
                var sql = @"
                    ;with
                        БазовыйТип        as (select id_node, [double_value] 'value' from assembly_tblValueHref WITH(NOLOCK) where [type] = 'БазовыйТип'),
	                    НомерТипаДаннах   as (select id_node, [double_value] 'value' from assembly_tblValueDouble WITH(NOLOCK) where [type] = 'НомерТипаДаннах'),
                        IsSystem          as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsSystem'),
                        IsAutoIncrement   as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsAutoIncrement'),
                        IsReadOnly	      as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsReadOnly'),
                        IsSetDefaultValue as (select id_node, [double_value] 'value' from assembly_tblValueBool WITH(NOLOCK) where [type] = 'IsSetDefaultValue'),
	                    ИмяТипаДанных     as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'ИмяТипаДанных'),
	                    Namespace         as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'КатегорияТипаДанных'),
	                    Описание		  as (select id_node, [string_value] 'value' from assembly_tblValueString WITH(NOLOCK) where [type] = 'НазваниеОбъекта'),
                        СписокТиповДанных as (select  N.id_node, БазовыйТип.value 'БазовыйТип' from assembly_tblNode N WITH(NOLOCK)  left join БазовыйТип WITH(NOLOCK) on N.id_node = БазовыйТип.id_node where N.id_node = @id_node)

                    select
                        СписокТиповДанных.id_node 'id_node',
                        isnull(Namespace.value,'') 'Namespace',
                        DeclaringType = 0,
                        isnull(НомерТипаДаннах.value,0)  'id_type',
                        isnull(И1.value,'') 'Name',
                        isnull(Описание.value, '') 'Описание',
                        isnull(И2.value,'') 'BaseType',
                        ReflectedType = '',
                        isnull(IsReadOnly.value,0)  'IsReadOnly',
                        isnull(IsSystem.value,0)  'IsSystem',
                        isnull(IsAutoIncrement.value,0)  'IsAutoIncrement',
                        isnull(IsSetDefaultValue.value,0)  'IsSetDefaultValue'
                    from 
                        СписокТиповДанных WITH(NOLOCK) 
                        left join Namespace WITH(NOLOCK) on СписокТиповДанных.id_node = Namespace.id_node
                        left join НомерТипаДаннах WITH(NOLOCK) on СписокТиповДанных.id_node = НомерТипаДаннах.id_node 
                        left join ИмяТипаДанных И1 WITH(NOLOCK) on СписокТиповДанных.id_node = И1.id_node
	                    left join ИмяТипаДанных И2 WITH(NOLOCK) on СписокТиповДанных.БазовыйТип = И2.id_node
                        left join Описание  WITH(NOLOCK) on СписокТиповДанных.id_node = Описание.id_node 
                        left join IsSystem WITH(NOLOCK) on СписокТиповДанных.id_node = IsSystem.id_node 
                        left join IsAutoIncrement WITH(NOLOCK) on СписокТиповДанных.id_node = IsAutoIncrement.id_node 
                        left join IsReadOnly WITH(NOLOCK) on СписокТиповДанных.id_node = IsReadOnly.id_node
                        left join IsSetDefaultValue WITH(NOLOCK) on СписокТиповДанных.id_node = IsSetDefaultValue.id_node";

                var command = (db.Connection as SqlConnection).CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 120;
                command.Parameters.AddWithValue("@id_node", id_node);
                new SqlDataAdapter(command).Fill(table);
                #endregion
                foreach (var item in table.Tables[0].AsEnumerable())
                {
                    var type = GetAssemblyAttribute(item);
                    if (Convert.IsDBNull(item["id_node"]))
                        throw new Exception(string.Format("Не определяется тип данных #{0:f0}", id_node));
                    type.TypeHashCode = GetTypeHashCode(item.Field<decimal>("id_node"), domain);
                    yield return type;
                }
                foreach (var attr in СписокАтрибутов(id_node, domain))
                {
                    yield return attr;
                }
            }
        }

        public static void КомпилироватьТипДанных(decimal id_node, string domain)
        {
            try
            {
                using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open) 
                            db.Connection.Open();

                        var items = СписокТиповДанных(id_node, domain).ToArray();
                        var type = items.First();

                        var command = ((SqlConnection)db.Connection).CreateCommand();
                        command.CommandTimeout = 120;
                        command.CommandText = @"delete from assembly_tblAttribute where id_parent = @id_parent or (id_parent = 0 and Name = @Name)";
                        command.Parameters.AddWithValue("@id_parent", type.id_type);
                        command.Parameters.AddWithValue("@Name", type.Name);
                        command.ExecuteNonQuery();
                        foreach (var item in items)
                        {
                            try
                            {
                                #region sql insert
                                command = ((SqlConnection)db.Connection).CreateCommand();
                                command.CommandText = string.Format(
                                @"insert into assembly_tblAttribute (
                                    [id_parent],
                                    [id_type],
                                    [ReflectedType],
                                    [BaseType],
                                    [HashCode],
                                    [TypeHashCode],
                                    [Описание],
                                    [Name],
                                    [Namespace],
                                    [MemberType],
                                    [RegisterType],
                                    [IsAutoIncrement],
                                    [IsReadOnly],
                                    [IsSetDefaultValue]) 
                                values (
                                    {0:f0},{1:f0},'{2}','{3}','{4}','{5}','{6}','{7}','{8}',{9},'{10}',@IsAutoIncrement,@IsReadOnly,@IsSetDefaultValue)",
                                    item.DeclaringType,
                                    item.id_type,
                                    item.ReflectedType,
                                    item.BaseType,
                                    item.HashCode,
                                    item.TypeHashCode,
                                    item.Описание,
                                    item.Name,
                                    item.Namespace,
                                    (int)item.MemberType,
                                    item.RegisterType.ToString());
                                command.Parameters.AddWithValue("@IsAutoIncrement", item.IsAutoIncrement);
                                command.Parameters.AddWithValue("@IsReadOnly", item.IsReadOnly);
                                //command.Parameters.AddWithValue("@IsSystem", item.IsSystem);
                                command.Parameters.AddWithValue("@IsSetDefaultValue", item.IsSetDefaultValue);
                                command.ExecuteNonQuery();
                                #endregion
                            }
                            catch (SqlException ex)
                            {
                                //игнорировать дубликаты
                                if (ex.Number == 2601) continue;
                                throw ex;
                            }
                        }
                    }
                    finally
                    {
                        db.Connection.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), "", domain, "КомпилироватьТипДанных",
                    id_node,
                    new Data.DataClient().ПолучитьЗначение<string>(id_node, "НазваниеОбъекта", Хранилище.Конфигурация, domain));
                throw ex;
            }
        }
        public static void КомпилироватьЗависимыеТипыДанных(string ТипДанных, string domain)
        {
            foreach (var item in new ConfigurationClient().СписокЗависимыхТипов(ТипДанных, domain))
            {
                try
                {

                    var query = new Query();
                    query.Типы.Add("ТипДанных");
                    query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИмяТипаДанных", Значение = item.Name });
                    query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = Convert.ToDecimal(ConfigurationClient.СистемныеПапки.РазделТипы), МаксимальнаяГлубина = 1 });
                    var node = new Data.DataClient().Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().SingleOrDefault();
                    if (node == null) continue;

                    Компилятор.КомпилироватьТипДанных(node.Field<decimal>("id_node"), domain);
                    Cache.Remove(Cache.Key<КешСписокАтрибутов>(domain, item.Name));
                }
                catch (Exception ex)
                {
                    ConfigurationClient.WindowsLog(ex.ToString(), "", domain, "КомпилироватьЗависимыеТипыДанных", ТипДанных);
                    throw ex;
                }
            }
        }
        public static void КомпилироватьКонфигурацию(string domain)
        {
            try
            {
                using (DataClasses.ClientDataContext db = new DataClasses.ClientDataContext(domain))
                {
                    try
                    {
                        if (db.Connection.State != ConnectionState.Open)
                            db.Connection.Open();

                        var command = ((SqlConnection)db.Connection).CreateCommand();
                        command.CommandTimeout = 120;
                        command.CommandText = "delete from assembly_tblAttribute";
                        command.ExecuteNonQuery();
                    }
                    finally
                    {
                        if (db.Connection != null)
                            db.Connection.Close();
                    }

                    foreach (var item in СписокРазделов(domain))
                    {
                        КомпилироватьТипДанных(item, domain);
                    }

                    var configuration = new ConfigurationClient();
                    foreach (var item in db.assembly_tblAttributes.Where(p => p.id_parent == 0 && p.MemberType == (int)MemberTypes.Object))
                    {
                        configuration.СохранитьЗначение(item.Name, "Тип.Описание", item.Описание, domain);
                        configuration.СохранитьЗначение(item.Name, "Тип.Имя", item.Name, domain);
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), "", domain);
                throw ex;
            }
        }
    };

    internal class CompileType : RosService.Configuration.Type
    {
        public decimal id_type { get; set; }

        public CompileType()
        {
        }
        public CompileType(DataRow row) : base(row)
        {
            id_type = row.Field<decimal>("id_type");
        }
    }
}
