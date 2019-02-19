using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Data;
using System.IO;

namespace System.Data
{
    public static class RosServiceExtensions
    {
        public static EnumerableRowCollection<DataRow> AsEnumerable(this TableValue source)
        {
            return source.Значение != null ? source.Значение.AsEnumerable() : null;
        }

        public static string AsXml(this DataTable source)
        {
            if (source != null)
            {
                if (string.IsNullOrEmpty(source.TableName))
                    source.TableName = "r";

                var sb = new StringBuilder();
                source.WriteXml(new StringWriter(sb), XmlWriteMode.WriteSchema);
                return sb.ToString();
            }
            return string.Empty;
        }
        public static string AsXml(this DataView source)
        {
            if (source == null)
                return string.Empty;

            return AsXml(source.Table);
        }

        public static T Convert<T>(this DataRow row, string columnName)
        {
            try
            {
                if (row == null)
                    return default(T);

                else if (row[columnName] is T)
                    return (T)row[columnName];
                else
                    return (T)System.Convert.ChangeType(row[columnName], typeof(T));
            }
            catch (ArgumentException)
            {
                return default(T);
            }
            catch
            {
                return default(T);
            }
        }
        public static T Convert<T>(this DataRowView row, string columnName)
        {
            return row.Row.Convert<T>(columnName);
        }
        public static T Convert<T>(this Dictionary<string, object> v, string columnName)
        {
            try
            {
                try
                {
                    if (v == null)
                    {
                        return default(T);
                    }
                    else if (v[columnName] is T)
                    {
                        return (T)v[columnName];
                    }
                    else
                    {
                        return (T)System.Convert.ChangeType(v[columnName], typeof(T));
                    }
                }
                catch
                {
                    return (T)Activator.CreateInstance(typeof(T));
                }
            }
            catch
            {
                return default(T);
            }
        }
    }
}
