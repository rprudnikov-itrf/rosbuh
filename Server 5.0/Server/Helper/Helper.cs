using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections;
using RosService.Intreface;
using RosService.Data;

namespace RosService.Helper
{
    public static class ConvertHelper
    {
        public static Dictionary<string, Value> ConvertDataValue(Dictionary<string, object> значения)
        {
            var values = new Dictionary<string, Value>();
            if (значения != null)
            {
                foreach (var item in значения)
                {
                    var value = new Value();
                    if (item.Value is DataTable)
                    {
                        var table = item.Value as DataTable;
                        table.AcceptChanges();
                        if (table != null && string.IsNullOrEmpty(table.TableName))
                            table.TableName = item.Key;

                        foreach (DataColumn column in table.Columns)
                            if (column.DataType == typeof(object))
                                throw new Exception("Таблица не может содержать колонок типа 'object'");

                        value.IsСписок = true;
                        value.SetTable(table);
                    }
                    else if (item.Value is DataView)
                    {
                        var table = (item.Value as DataView).Table;
                        table.AcceptChanges();
                        if (table != null && string.IsNullOrEmpty(table.TableName))
                            table.TableName = item.Key;

                        foreach (DataColumn column in table.Columns)
                            if (column.DataType == typeof(object))
                                throw new Exception("Таблица не может содержать колонок типа 'object'");

                        value.IsСписок = true;
                        value.SetTable(table);
                    }
                    else if (!(item.Value is string) && item.Value is System.Collections.IEnumerable)
                    {
                        var rows = (item.Value as System.Collections.IEnumerable).Cast<object>();
                        if (rows != null && rows.Count() > 0)
                        {
                            var table = new DataTable("Values");
                            table.Columns.Add("Value", rows.ElementAt(0).GetType());
                            foreach (var r in rows)
                            {
                                table.Rows.Add(new object[] { r });
                            }

                            value.IsСписок = true;
                            value.SetTable(table);
                        }
                    }
                    else
                    {
                        value.Значение = System.Convert.IsDBNull(item.Value) ? null : item.Value;
                    }
                    values.Add(item.Key, value);
                }
            }
            return values;
        }
        public static Dictionary<string, object> ConvertDataValue(Dictionary<string, Value> значения)
        {
            var values = new Dictionary<string, object>();
            foreach (var item in значения)
            {
                values.Add(item.Key, item.Value.IsСписок ? item.Value.Таблица : item.Value.Значение);
            }
            return values;
        }
    }
}

