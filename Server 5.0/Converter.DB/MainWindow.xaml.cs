using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Data;

namespace Converter.DB
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string Current
        {
            get { return (string)GetValue(CurrentProperty); }
            set { SetValue(CurrentProperty, value); }
        }
        public static readonly DependencyProperty CurrentProperty =
            DependencyProperty.Register("Current", typeof(string), typeof(MainWindow), new UIPropertyMetadata(null));

        public int Count
        {
            get { return (int)GetValue(CountProperty); }
            set { SetValue(CountProperty, value); }
        }
        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register("Count", typeof(int), typeof(MainWindow), new UIPropertyMetadata(0));


        public int Row
        {
            get { return (int)GetValue(RowProperty); }
            set { SetValue(RowProperty, value); }
        }
        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register("Row", typeof(int), typeof(MainWindow), new UIPropertyMetadata(0));



        private Dictionary<string, int> types;



        public MainWindow()
        {
            InitializeComponent();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Start.IsEnabled = false;

            var __ConnectTo = ConnectToWithoutDb;
            var __db = ToDB.Text;
            var __path = PART_Path.Text + __db + @"\";
            this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
            {
                System.Threading.Tasks.Task.Factory.StartNew(delegate()
                {
                    var isDb = DbCheck(__ConnectTo, __db);
                    if (isDb)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate 
                        {
                            if (MessageBox.Show("Внимание все данные будут удалены, продолжить?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                start();
                            else
                                Start.IsEnabled = true;
                        });
                    }
                    else
                    {
                       this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                       {
                           if (MessageBox.Show(string.Format("Указанная база не существует, создать? Убедитесь, что каталог '{0}' существует на сервере.", __path), 
                               "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                           {
                               System.Threading.Tasks.Task.Factory.StartNew(delegate()
                               {
                                   CreateDb(__ConnectTo, __db, __path);
                                   this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { start(); });
                               });
                           }
                           else
                           {
                               Start.IsEnabled = true;
                           }
                       });
                    }
                });
            });
        }


        private decimal id_node = 0;
        private void start()
        {
            var __ConnectFrom = ConnectFrom;
            var __ConnectTo = ConnectTo;
            var __Stage0 = Stage0.IsChecked.Value;
            var __Stage1 = Stage1.IsChecked.Value;
            var __Stage2 = Stage2.IsChecked.Value;
            var __tblNode = tblNode.IsChecked.Value;
            var __tblValue = tblValue.IsChecked.Value;
            var __db = ToDB.Text;
            System.Threading.Tasks.Task.Factory.StartNew(delegate()
            {
                try
                {
                    using (var clientFrom = new SqlConnection(__ConnectFrom))
                    using (var clientTo = new SqlConnection(__ConnectTo))
                    {
                        try
                        {
                            clientFrom.Open();
                            clientTo.Open();

                            #region Загрузить типы
                            types = new Dictionary<string, int>();
                            var sql = clientFrom.CreateCommand();
                            sql.CommandText = "SELECT [Name],[MemberType] FROM [assembly_tblAttribute] where [ID_PARENT] = 0";
                            sql.CommandTimeout = 120;
                            var reader = sql.ExecuteReader();
                            try
                            {
                                while (reader.Read())
                                {
                                    if (!types.ContainsKey(Convert.ToString(reader["Name"])))
                                        types.Add(Convert.ToString(reader["Name"]), Convert.ToInt32(reader["MemberType"]));
                                }
                            }
                            finally
                            {
                                if (reader != null)
                                    reader.Close();
                            }
                            #endregion

                            //создать типы
                            if (__Stage0)
                                Types(clientFrom, clientTo);

                            if (__Stage2)
                            {
                                if(__tblNode)
                                    Nodes(clientFrom, clientTo, "assembly_", __ConnectTo);
                                if (__tblValue)
                                    Values(clientFrom, clientTo, "assembly_", __ConnectTo);
                            }

                            if (__Stage1)
                            {
                                if (__tblNode)
                                    Nodes(clientFrom, clientTo, "", __ConnectTo);
                                if (__tblValue)
                                    Values(clientFrom, clientTo, "", __ConnectTo);
                            }
                        }
                        finally
                        {
                            if (clientFrom != null)
                                clientFrom.Close();
                            if (clientTo != null)
                                clientTo.Close();
                        }
                    }

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        Count = 0;
                        ProgressBar1.Value = 0;
                        Current = null;
                        MessageBox.Show("Готово!");
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        MessageBox.Show(ex.ToString());
                        System.IO.File.WriteAllText("trace.txt", id_node.ToString() + "\n\n" + ex.ToString());
                        Start.IsEnabled = true;
                    });
                }
            });
        }
        private bool DbCheck(string ConnectTo, string db)
        {
            if (string.IsNullOrEmpty(db))
                throw new Exception("Не указан база данных 'Куда'");

            using (var client = new SqlConnection(ConnectTo))
            {
                try
                {
                    client.Open();

                    var sqlCommand = client.CreateCommand();
                    sqlCommand.CommandText = "select isnull(count(*), 0) from sys.databases where name = @db";
                    sqlCommand.CommandTimeout = 360;
                    sqlCommand.Parameters.AddWithValue("@db", db);
                    if (Convert.ToInt32(sqlCommand.ExecuteScalar()) == 0)
                    {
                        return false;
                    }
                }
                finally
                {
                    if (client != null)
                        client.Close();
                }
            }
            return true;
        }
        private void CreateDb(string ConnectTo, string db, string path)
        {
            if (string.IsNullOrEmpty(db))
                throw new Exception("Не указан база данных 'Куда'");

            using (var client = new SqlConnection(ConnectTo))
            {
                try
                {
                    client.Open();

                    var sqls = Converter.DB.Properties.Resources.create_sql.Split(new string[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sql in sqls)
                        ExecuteQuery(client,string.Format(sql.Trim(), db, path));

                    client.ChangeDatabase(db);

                    sqls = Converter.DB.Properties.Resources.sql.Split(new string[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sql in sqls)
                        ExecuteQuery(client, sql.Trim());
                }
                catch(Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
                finally
                {
                    if (client != null)
                        client.Close();
                }
            }
        }
        private void Types(SqlConnection clientFrom, SqlConnection clientTo)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                Count = 0;
                ProgressBar1.Value = 0;
            });

            ExecuteQuery(clientTo, "DELETE FROM [assembly_tblAttribute]");

            var sql = clientFrom.CreateCommand();
            sql.CommandText = "SELECT COUNT(*) FROM [assembly_tblAttribute]";
            sql.CommandTimeout = 120;
            SetCurrent(sql.CommandText);
            var count = Convert.ToInt32(sql.ExecuteScalar());

            sql = clientFrom.CreateCommand();
            sql.CommandText = "SELECT * FROM [assembly_tblAttribute]";
            sql.CommandTimeout = 120;
            SetCurrent(sql.CommandText);
            var reader = sql.ExecuteReader();

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { Count = count; });


            try
            {
                var oRow = 0;
                while (reader.Read())
                {
                    var sqlInsert = clientTo.CreateCommand();
                    sqlInsert.CommandTimeout = 120;
                    sqlInsert.CommandText = @"INSERT INTO [assembly_tblAttribute]
           ([Namespace]
           ,[id_parent]
           ,[id_type]
           ,[HashCode]
           ,[TypeHashCode]
           ,[Name]
           ,[Описание]
           ,[MemberType]
           ,[RegisterType]
           ,[BaseType]
           ,[ReflectedType]
           ,[IsReadOnly]
           ,[IsAutoIncrement]
           ,[IsSystem]
           ,[IsSetDefaultValue])
     VALUES
           (@Namespace
           ,@id_parent
           ,@id_type
           ,@HashCode
           ,@TypeHashCode
           ,@Name
           ,@Описание
           ,@MemberType
           ,@RegisterType
           ,@BaseType
           ,@ReflectedType
           ,@IsReadOnly
           ,@IsAutoIncrement
           ,@IsSystem
           ,@IsSetDefaultValue)";
                    sqlInsert.Parameters.AddWithValue("@Namespace", reader["Namespace"]);
                    sqlInsert.Parameters.AddWithValue("@id_parent", reader["id_parent"]);
                    sqlInsert.Parameters.AddWithValue("@id_type", reader["id_type"]);
                    sqlInsert.Parameters.AddWithValue("@HashCode", reader["HashCode"]);
                    sqlInsert.Parameters.AddWithValue("@TypeHashCode", reader["TypeHashCode"]);
                    sqlInsert.Parameters.AddWithValue("@Name", reader["Name"]);
                    sqlInsert.Parameters.AddWithValue("@Описание", reader["Описание"]);
                    sqlInsert.Parameters.AddWithValue("@MemberType", reader["MemberType"]);
                    sqlInsert.Parameters.AddWithValue("@RegisterType", reader["RegisterType"]);
                    sqlInsert.Parameters.AddWithValue("@BaseType", reader["BaseType"]);
                    sqlInsert.Parameters.AddWithValue("@ReflectedType", reader["ReflectedType"]);
                    sqlInsert.Parameters.AddWithValue("@IsReadOnly", reader["IsReadOnly"]);
                    sqlInsert.Parameters.AddWithValue("@IsAutoIncrement", reader["IsAutoIncrement"]);
                    sqlInsert.Parameters.AddWithValue("@IsSystem", reader["IsSystem"]);
                    sqlInsert.Parameters.AddWithValue("@IsSetDefaultValue", reader["IsSetDefaultValue"]);
                    sqlInsert.ExecuteNonQuery();

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        if (oRow % 100 == 0)
                        {
                            Row = oRow;
                            ProgressBar1.Value = Convert.ToInt32((double)oRow / (double)count * 100);
                        }
                    });

                    oRow++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        private void Nodes(SqlConnection clientFrom, SqlConnection clientTo, string prefix, string __ConnectTo)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                Count = 0;
                ProgressBar1.Value = 0;
            });

            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblNode]", prefix));

            var sql = clientFrom.CreateCommand();
            sql.CommandText = string.Format("SELECT COUNT(*) FROM [{0}tblNode]", prefix);
            sql.CommandTimeout = 120;
            SetCurrent(sql.CommandText);
            var count = Convert.ToInt32(sql.ExecuteScalar());

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { Count = count;  });

            #region sqlInsert
//            var sqlInsert = clientTo.CreateCommand();
//            sqlInsert.CommandTimeout = 120;
//            sqlInsert.Parameters.AddWithValue("@id_node", null);
//            sqlInsert.Parameters.AddWithValue("@id_parent", null);
//            sqlInsert.Parameters.AddWithValue("@HashCode", null);
//            sqlInsert.Parameters.AddWithValue("@type", null);
//            sqlInsert.Parameters.AddWithValue("@GuidCode", null);
//            sqlInsert.CommandText = string.Format(@"INSERT INTO [{0}tblNode]
//    ([id_node]
//    ,[id_parent]
//    ,[HashCode]
//    ,[type]
//    ,[hide]
//    ,[GuidCode])
//VALUES
//    (@id_node
//    ,@id_parent
//    ,@HashCode
//    ,@type
//    ,0
//    ,@GuidCode)", prefix);
            #endregion


            try
            {
                var sqlBulkCopy = new SqlBulkCopy(__ConnectTo, SqlBulkCopyOptions.KeepIdentity);
                sqlBulkCopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblNode", prefix);

                var command1 = (clientFrom).CreateCommand();
                command1.CommandText = string.Format(@"select * from {0}tblNode", prefix);
                command1.CommandType = CommandType.Text;
                command1.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;

                //var oRow = 0;
                //var pages = 5000L;
                //for (long i = 0; i < (long)pages; i++)
                {
                    //command1.Parameters["@CurrentPage"].Value = i;

                    using (var dsCache = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                    {
                        using (var adapter = new SqlDataAdapter(command1))
                        {
                            adapter.AcceptChangesDuringFill = false;
                            adapter.AcceptChangesDuringUpdate = false;
                            adapter.Fill(dsCache);
                        }

                        if (dsCache.Tables.Count > 0)
                        {
                            var table = dsCache.Tables[0];
                            foreach (DataColumn column in table.Columns)
                            {
                                sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                            }
                            sqlBulkCopy.WriteToServer(table);
                        }

                        //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        //{
                        //    if (oRow % 100 == 0)
                        //    {
                        //        Row = oRow;
                        //        ProgressBar1.Value = Convert.ToInt32((double)oRow / (double)count * 100);
                        //    }
                        //});

                        //oRow += (int)pages;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            //try
            //{
            //    var sqlTmp = clientTo.CreateCommand();
            //    sqlTmp.CommandText = string.Format(@"SET IDENTITY_INSERT [{0}tblNode] ON", prefix);
            //    sqlTmp.ExecuteNonQuery();

            //    var oRow = 0;
            //    while (reader.Read())
            //    {
            //        sqlInsert.Parameters["@id_node"] = new SqlParameter("@id_node", reader["id_node"]);
            //        sqlInsert.Parameters["@id_parent"] = new SqlParameter("@id_parent", reader["id_parent"]);
            //        sqlInsert.Parameters["@HashCode"] = new SqlParameter("@HashCode", reader["HashCode"]);
            //        sqlInsert.Parameters["@type"] = new SqlParameter("@type", reader["type"]);
            //        sqlInsert.Parameters["@GuidCode"] = new SqlParameter("@GuidCode", reader["GuidCode"]);
            //        sqlInsert.ExecuteNonQuery();

            //        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            //        {
            //            if (oRow % 100 == 0)
            //            {
            //                Row = oRow;
            //                ProgressBar1.Value = Convert.ToInt32((double)oRow / (double)count * 100);
            //            }
            //        });

            //        oRow++;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
            //finally
            //{
            //    if (reader != null)
            //        reader.Close();

            //    var sqlTmp = clientTo.CreateCommand();
            //    sqlTmp.CommandText = string.Format(@"SET IDENTITY_INSERT [{0}tblNode] OFF", prefix);
            //    sqlTmp.ExecuteNonQuery();
            //}
        }

        private object lockObject = new System.Object();
        private void Values(SqlConnection clientFrom, SqlConnection clientTo, string prefix, string __ConnectTo)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                Count = 0;
                ProgressBar1.Value = 0;
            });

            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueBool]", prefix));
            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueByte]", prefix));
            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueDate]", prefix));
            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueDouble]", prefix));
            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueHref]", prefix));
            ExecuteQuery(clientTo, string.Format("DELETE FROM [{0}tblValueString]", prefix));

            var sql = clientFrom.CreateCommand();
            sql.CommandText = string.Format("SELECT COUNT(*) FROM [{0}tblValue]", prefix);
            sql.CommandTimeout = 120;
            SetCurrent(sql.CommandText);
            var count = Convert.ToInt32(sql.ExecuteScalar());

            sql = clientFrom.CreateCommand();
            sql.CommandText = string.Format("SELECT * FROM [{0}tblValue] ORDER BY [id_value] ASC", prefix);
            SetCurrent(sql.CommandText);
            sql.CommandTimeout = 120;

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { Count = count; });

            using (var dsCache = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
            {
                using (var adapter = new SqlDataAdapter(sql))
                {
                    adapter.AcceptChangesDuringFill = false;
                    adapter.AcceptChangesDuringUpdate = false;
                    adapter.Fill(dsCache);
                }

                var import = dsCache.Tables[0].AsEnumerable();

                var tblValueString = import.Where(p => types.ContainsKey(p.Field<string>("type")) && types[p.Field<string>("type")] == 2).CopyToDataTable();
                SaveDataTable(__ConnectTo, prefix, tblValueString, 2);

                var tblValueBool = import.Where(p => types.ContainsKey(p.Field<string>("type")) && types[p.Field<string>("type")] == 6).CopyToDataTable();
                SaveDataTable(__ConnectTo, prefix, tblValueBool, 6);

                var tblValueDate = import.Where(p => types.ContainsKey(p.Field<string>("type")) && types[p.Field<string>("type")] == 5).CopyToDataTable();
                SaveDataTable(__ConnectTo, prefix, tblValueDate, 5);

                var tblValueDouble = import.Where(p => types.ContainsKey(p.Field<string>("type")) && (types[p.Field<string>("type")] == 3 || types[p.Field<string>("type")] == 4)).CopyToDataTable();
                SaveDataTable(__ConnectTo, prefix, tblValueDouble, 3);

                var tblValueHref = import.Where(p => types.ContainsKey(p.Field<string>("type")) && types[p.Field<string>("type")] == 7).CopyToDataTable();
                SaveDataTable(__ConnectTo, prefix, tblValueHref, 7);

                var tblValueByteCount = 0;
                if (prefix == "assembly_")
                {
                    var tblValueByte = import.Where(p => types.ContainsKey(p.Field<string>("type")) && types[p.Field<string>("type")] == 9).CopyToDataTable();
                    SaveDataTable(__ConnectTo, prefix, tblValueByte, 9);
                    tblValueByteCount = tblValueByte.Rows.Count;
                }

                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    MessageBox.Show(string.Format("Потеря данных: {0}", import.Count()
                        - tblValueString.Rows.Count
                        - tblValueBool.Rows.Count
                        - tblValueDate.Rows.Count
                        - tblValueDouble.Rows.Count
                        - tblValueHref.Rows.Count
                        - tblValueByteCount));
                });
            }
        }

        private void SaveDataTable(string __ConnectTo, string prefix, DataTable table, int member)
        {
            using (var _clientTo = new SqlConnection(__ConnectTo))
            {
                #region hide
                //var sqlInsert = _clientTo.CreateCommand();
                //sqlInsert.CommandTimeout = 120;
                //sqlInsert.Parameters.AddWithValue("@id_node", i["id_node"]).SqlDbType = System.Data.SqlDbType.Decimal;
                //sqlInsert.Parameters.AddWithValue("@type", i["type"]).SqlDbType = System.Data.SqlDbType.VarChar;

                //#region tbl
                //var member = types[Convert.ToString(i["type"])];
                //var tbl = string.Empty;
                //switch (member)
                //{
                //    case 2:
                //        tbl = "tblValueString";
                //        break;

                //    case 3:
                //    case 4:
                //        tbl = "tblValueDouble";
                //        break;

                //    case 5:
                //        tbl = "tblValueDate";
                //        break;

                //    case 6:
                //        tbl = "tblValueBool";
                //        break;

                //    case 7:
                //        tbl = "tblValueHref";
                //        break;

                //    case 9:
                //        tbl = "tblValueByte";
                //        break;

                //    default:
                //        return;
                //}
                //#endregion

                //#region column
                //var column = string.Empty;
                //switch (member)
                //{
                //    case 2:
                //        column = "string_value";
                //        sqlInsert.Parameters.AddWithValue("@value", i[column]).SqlDbType = System.Data.SqlDbType.VarChar;
                //        break;

                //    case 3:
                //    case 4:
                //    case 6:
                //    case 7:
                //        column = "double_value";
                //        sqlInsert.Parameters.AddWithValue("@value", i[column]).SqlDbType = System.Data.SqlDbType.Decimal;
                //        break;

                //    case 5:
                //        column = "datetime_value";
                //        sqlInsert.Parameters.AddWithValue("@value", i[column]).SqlDbType = System.Data.SqlDbType.DateTime;
                //        break;

                //    case 9:
                //        column = "byte_value";
                //        sqlInsert.Parameters.AddWithValue("@value", i[column]).SqlDbType = System.Data.SqlDbType.VarBinary;
                //        break;

                //    default:
                //        return;
                //}
                //#endregion

                //if (Convert.IsDBNull(i[column]))
                //    return;

                //switch (member)
                //{
                //    case 2:
                //        sqlInsert.CommandText = string.Format(@"INSERT INTO [{0}{1}] ([id_node],[type],[{2}],[string_value_index],[hide]) VALUES (@id_node,@type,@value,@string_value_index,0)", prefix, tbl, column);
                //        sqlInsert.Parameters.AddWithValue("@string_value_index", i["string_value_index"]);
                //        break;

                //    case 7:
                //        sqlInsert.CommandText = string.Format(@"INSERT INTO [{0}{1}] ([id_node],[type],[{2}],[string_value_index],[hide]) VALUES (@id_node,@type,@value,@string_value_index,0)", prefix, tbl, column);
                //        sqlInsert.Parameters.AddWithValue("@string_value_index", i["string_value_index"]);
                //        break;

                //    default:
                //        sqlInsert.CommandText = string.Format(@"INSERT INTO [{0}{1}] ([id_node],[type],[{2}],[hide]) VALUES (@id_node,@type,@value,0)", prefix, tbl, column);
                //        break;
                //}
                #endregion

                var sqlBulkCopy = new SqlBulkCopy(__ConnectTo, SqlBulkCopyOptions.KeepIdentity);
                sqlBulkCopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                if (table.Rows.Count > 0)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        Row += table.Rows.Count;
                    });

                    sqlBulkCopy.ColumnMappings.Add("id_node", "id_node");
                    sqlBulkCopy.ColumnMappings.Add("type", "type");
                    sqlBulkCopy.ColumnMappings.Add("hide", "hide");

                    switch (member)
                    {
                        case 2:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueString", prefix);
                            sqlBulkCopy.ColumnMappings.Add("id_value", "id_value");
                            sqlBulkCopy.ColumnMappings.Add("string_value", "string_value");
                            sqlBulkCopy.ColumnMappings.Add("string_value_index", "string_value_index");
                            break;

                        case 3:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueDouble", prefix);
                            sqlBulkCopy.ColumnMappings.Add("double_value", "double_value");
                            break;

                        case 6:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueBool", prefix);
                            table.Columns.Add("bool_value", typeof(bool));
                            foreach (var item in table.AsEnumerable())
                                item["bool_value"] = !Convert.IsDBNull(item["double_value"]) && item.Field<decimal>("double_value") > 0;
                            sqlBulkCopy.ColumnMappings.Add("bool_value", "double_value");
                            break;

                        case 7:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueHref", prefix);
                            sqlBulkCopy.ColumnMappings.Add("id_value", "id_value");
                            sqlBulkCopy.ColumnMappings.Add("double_value", "double_value");
                            sqlBulkCopy.ColumnMappings.Add("string_value_index", "string_value_index");
                            break;

                        case 5:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueDate", prefix);
                            sqlBulkCopy.ColumnMappings.Add("datetime_value", "datetime_value");
                            break;

                        case 9:
                            sqlBulkCopy.DestinationTableName = string.Format(@"{0}tblValueByte", prefix);
                            sqlBulkCopy.ColumnMappings.Add("byte_value", "byte_value");
                            break;                            
                    }
                    sqlBulkCopy.WriteToServer(table);
                }
            }
        }

        private void ExecuteQuery(SqlConnection client, string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return;

            SetCurrent(sql);

            var sqlCommand = client.CreateCommand();
            sqlCommand.CommandText = sql;
            sqlCommand.CommandTimeout = 6000;
            sqlCommand.ExecuteNonQuery();
        }
        private void SetCurrent(string txt)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                Current = txt;
            });
        }

        private string ConnectFrom
        {
            get
            {
                if (string.IsNullOrEmpty(FromServer.Text))
                    throw new Exception("Не указан сервер 'От куда'");
                if (string.IsNullOrEmpty(FromLogin.Text))
                    throw new Exception("Не указан логин 'От куда'");
                if (string.IsNullOrEmpty(FromPassword.Text))
                    throw new Exception("Не указан пароль 'От куда'");
                if (string.IsNullOrEmpty(FromDB.Text))
                    throw new Exception("Не указан база данных 'От куда'");

                return string.Format("Data Source={0};User={1};Password={2};Database={3};",
                    FromServer.Text,
                    FromLogin.Text,
                    FromPassword.Text,
                    FromDB.Text);
            }
        }
        private string ConnectTo
        {
            get
            {
                if (string.IsNullOrEmpty(ToServer.Text))
                    throw new Exception("Не указан сервер 'Куда'");
                if (string.IsNullOrEmpty(ToLogin.Text))
                    throw new Exception("Не указан логин 'Куда'");
                if (string.IsNullOrEmpty(ToPassword.Text))
                    throw new Exception("Не указан пароль 'Куда'");
                if (string.IsNullOrEmpty(ToDB.Text))
                    throw new Exception("Не указан база данных 'Куда'");

                return string.Format("Data Source={0};User={1};Password={2};Database={3};",
                    ToServer.Text,
                    ToLogin.Text,
                    ToPassword.Text,
                    ToDB.Text);
            }
        }
        private string ConnectToWithoutDb
        {
            get
            {
                if (string.IsNullOrEmpty(ToServer.Text))
                    throw new Exception("Не указан сервер 'Куда'");
                if (string.IsNullOrEmpty(ToLogin.Text))
                    throw new Exception("Не указан логин 'Куда'");
                if (string.IsNullOrEmpty(ToPassword.Text))
                    throw new Exception("Не указан пароль 'Куда'");

                return string.Format("Data Source={0};User={1};Password={2};",
                    ToServer.Text,
                    ToLogin.Text,
                    ToPassword.Text);
            }
        }
    }
}
