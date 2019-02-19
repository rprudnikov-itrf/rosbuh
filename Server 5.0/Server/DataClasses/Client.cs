using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Configuration;

namespace RosService.DataClasses
{
    partial class ClientDataContext
    {
        private static readonly object lockObject = new System.Object();
        private static readonly Dictionary<string, string> ConnectionString = new Dictionary<string, string>();
        public static string GetConnectionString(string domain, string pattern, string table)
        {
            var key = domain + pattern;
            if (!string.IsNullOrEmpty(table)) 
                key += "." + table;

            if (!ConnectionString.ContainsKey(key))
            {
                lock (lockObject)
                {
                    var connectionStrings = ConfigurationManager.ConnectionStrings[key];
                    if (connectionStrings == null && !string.IsNullOrEmpty(pattern) && !string.IsNullOrEmpty(table))
                        connectionStrings = ConfigurationManager.ConnectionStrings["default" + pattern + "." + table];

                    if (connectionStrings == null && !string.IsNullOrEmpty(pattern))
                        connectionStrings = ConfigurationManager.ConnectionStrings[domain + pattern];

                    if (connectionStrings == null && !string.IsNullOrEmpty(pattern))
                        connectionStrings = ConfigurationManager.ConnectionStrings["default" + pattern];

                    if (connectionStrings == null)
                        connectionStrings = ConfigurationManager.ConnectionStrings[domain];

                    if (connectionStrings == null)
                        connectionStrings = ConfigurationManager.ConnectionStrings["default"];

                    if (connectionStrings == null)
                        throw new Exception("Не указана строка подключения к базе данных, добавьте в .config '<add name=\"default\" connectionString=\"Data Source=localhost;Database={0};User=***;Password=***;\" providerName=\"System.Data.SqlClient\" />'");

                    ConnectionString[key] = string.Format(connectionStrings.ConnectionString, domain);
                }
            }
            return ConnectionString[key];
        }
        //public static string GetConnectionString(string domain, string pattern, string table)
        //{
        //    var key = domain + pattern;
        //    if (!string.IsNullOrEmpty(table)) key += "." + table;

        //    if (!ConnectionString.ContainsKey(key))
        //    {
        //        lock (lockObject)
        //        {
        //            var connectionStrings = ConfigurationManager.ConnectionStrings[key] ?? ConfigurationManager.ConnectionStrings["default" + pattern];

        //            if (connectionStrings == null)
        //            {
        //                if (string.IsNullOrEmpty(pattern))
        //                    throw new Exception("Не указана строка подключения к базе данных, добавьте в .config '<add name=\"default\" connectionString=\"Data Source=localhost;Database={0};User=***;Password=***;\" providerName=\"System.Data.SqlClient\" />'");
        //                else
        //                    return GetConnectionString(domain, null, null);
        //            }

        //            ConnectionString[key] = string.Format(connectionStrings.ConnectionString, domain);
        //        }
        //    }
        //    return ConnectionString[key];
        //}

        public ClientDataContext(string domain) :
            base(string.Empty, mappingSource)
        {
            this.Connection.ConnectionString = GetConnectionString(domain, null, null);
        }
        public ClientDataContext(string domain, string pattern, string table) :
            base(string.Empty, mappingSource)
        {
            this.Connection.ConnectionString = GetConnectionString(domain, pattern, table);
        }
        public ClientDataContext(string domain, bool Async) :
            base(string.Empty, mappingSource)
        {
            this.Connection.ConnectionString = GetConnectionString(domain, null, null) + (Async ? ";Async=true;" : string.Empty);
        }
    }
}
