using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using RosService.Data;
using System.Collections;

namespace RosService
{
    public static class Helper
    {
        public static string РусскийВТранслит(string value)
        {
            Dictionary<string, string> words = new Dictionary<string, string>();
            words.Add("а", "a");
            words.Add("б", "b");
            words.Add("в", "v");
            words.Add("г", "g");
            words.Add("д", "d");
            words.Add("е", "e");
            words.Add("ё", "yo");
            words.Add("ж", "zh");
            words.Add("з", "z");
            words.Add("и", "i");
            words.Add("й", "j");
            words.Add("к", "k");
            words.Add("л", "l");
            words.Add("м", "m");
            words.Add("н", "n");
            words.Add("о", "o");
            words.Add("п", "p");
            words.Add("р", "r");
            words.Add("с", "s");
            words.Add("т", "t");
            words.Add("у", "u");
            words.Add("ф", "f");
            words.Add("х", "h");
            words.Add("ц", "c");
            words.Add("ч", "ch");
            words.Add("ш", "sh");
            words.Add("щ", "sch");
            words.Add("ъ", "j");
            words.Add("ы", "i");
            words.Add("ь", "j");
            words.Add("э", "e");
            words.Add("ю", "yu");
            words.Add("я", "ya");
            words.Add("А", "A");
            words.Add("Б", "B");
            words.Add("В", "V");
            words.Add("Г", "G");
            words.Add("Д", "D");
            words.Add("Е", "E");
            words.Add("Ё", "Yo");
            words.Add("Ж", "Zh");
            words.Add("З", "Z");
            words.Add("И", "I");
            words.Add("Й", "J");
            words.Add("К", "K");
            words.Add("Л", "L");
            words.Add("М", "M");
            words.Add("Н", "N");
            words.Add("О", "O");
            words.Add("П", "P");
            words.Add("Р", "R");
            words.Add("С", "S");
            words.Add("Т", "T");
            words.Add("У", "U");
            words.Add("Ф", "F");
            words.Add("Х", "H");
            words.Add("Ц", "C");
            words.Add("Ч", "Ch");
            words.Add("Ш", "Sh");
            words.Add("Щ", "Sch");
            words.Add("Ъ", "J");
            words.Add("Ы", "I");
            words.Add("Ь", "J");
            words.Add("Э", "E");
            words.Add("Ю", "Yu");
            words.Add("Я", "Ya");
            foreach (KeyValuePair<string, string> pair in words)
            {
                value = value.Replace(pair.Key, pair.Value);
            }
            return value;
        }
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


        public static class Web
        {
            public static DataTable Поиск(Query запрос)
            {
                return Поиск(запрос, Хранилище.Оперативное);
            }
            public static DataTable Поиск(Query запрос, Хранилище хранилище)
            {
                using (RosService.Client client = new RosService.Client())
                {
                    var table = client.Архив.Поиск(запрос, хранилище).Значение;
                    foreach (DataColumn item in table.Columns)
                    {
                        if (!item.ColumnName.Contains('.')) continue;
                        item.ColumnName = item.ColumnName.Replace('.', '_');
                    }
                    return table;
                }
            }
        }
    }
}

