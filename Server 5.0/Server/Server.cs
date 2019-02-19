using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Data.SqlClient;
using System.Data;
using System.IO;


namespace RosService
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
        AddressFilterMode = AddressFilterMode.Any,
        ConcurrencyMode = ConcurrencyMode.Multiple,
        UseSynchronizationContext = false,
        ConfigurationName = "RosService.Server")]
    public partial class ServerClient
    {
        public void АрхивироватьФайлы()
        {
            Configuration.ConfigurationClient.WindowsLog("Архивирование файлов начато...", string.Empty, "system");

            foreach (var domain in new Configuration.ConfigurationClient().СписокДоменов())
            {
                try
                {
                    var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"], domain);
                    if (!Directory.Exists(path))
                        continue;

                    var pathArhiv = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["АрхивХранилище"], domain);
                    if (!Directory.Exists(pathArhiv))
                        Directory.CreateDirectory(pathArhiv);

                    using (var db = new RosService.DataClasses.ClientDataContext(domain))
                    using (var ds = new DataSet() { RemotingFormat = SerializationFormat.Binary, EnforceConstraints = false })
                    {
                        var command = (db.Connection as SqlConnection).CreateCommand();
                        command.CommandTimeout = 300;
                        command.CommandText = @"
                        select [string_value_index] 'ПолноеИмяФайла' from tblValueString where [type] = 'ПолноеИмяФайла'
                        union
                        select [string_value_index] 'ПолноеИмяФайла' from assembly_tblValueString where [type] = 'ПолноеИмяФайла'
                        ";

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.AcceptChangesDuringFill = false;
                            adapter.AcceptChangesDuringUpdate = false;
                            adapter.Fill(ds);
                        }

                        //создать папку
                        var hashFiles = new HashSet<string>(ds.Tables[0].AsEnumerable().Select(p => p.Field<string>("ПолноеИмяФайла")).Distinct());
                        foreach (var item in Directory.GetFiles(path))
                        {
                            var filename = Path.GetFileName(item);
                            if (hashFiles.Contains(filename))
                                continue;

                            //переместить в архивную папку
                            File.Move(item, Path.Combine(pathArhiv, filename));
                        }
                    }
                }
                catch(Exception ex)
                {
                    Configuration.ConfigurationClient.WindowsLog("Архивирование файлов: " + domain, string.Empty, "system", ex.ToString());
                }
            }
            Configuration.ConfigurationClient.WindowsLog("Архивирование файлов завершено", string.Empty, "system");
        }
    }
}
