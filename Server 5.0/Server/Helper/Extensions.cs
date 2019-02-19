using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Intreface;
using RosService.Data;

namespace System.Data
{
    public static class RosServiceExtensions
    {
        public static EnumerableRowCollection<DataRow> AsEnumerable(this TableValue source)
        {
            return source.Значение != null ? source.Значение.AsEnumerable() : null;
        }

        public static T Convert<T>(this DataRow row, string columnName)
        {
            try
            {
                if (row[columnName] is T)
                    return (T)row[columnName];
                else
                    return (T)System.Convert.ChangeType(row[columnName], typeof(T));
            }
            catch (ArgumentException ex)
            {
                throw ex;
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
            return (T)v[columnName];
        }


        public static bool IsGuid(this string source)
        {
            //b05b3c37-6e56-e211-bb73-0030487e046b
            return source.Length == 36
                && source.ElementAt(8) == '-'
                && source.ElementAt(13) == '-'
                && source.ElementAt(18) == '-'
                && source.ElementAt(23) == '-';
        }
    }
}
