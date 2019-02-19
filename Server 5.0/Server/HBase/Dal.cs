using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RosService.HBaseCore
{
    class Dal
    {
        #region РусскийВТранслит
        static object lock_РусскийВТранслит = new object();

        static Dictionary<string, string> _words;
        static Dictionary<string, string> words
        {
            get
            {
                lock (lock_РусскийВТранслит)
                {
                    if (_words == null)
                    {
                        _words = new Dictionary<string, string>();

                        _words.Add("а", "a");
                        _words.Add("б", "b");
                        _words.Add("в", "v");
                        _words.Add("г", "g");
                        _words.Add("д", "d");
                        _words.Add("е", "e");
                        _words.Add("ё", "yo");
                        _words.Add("ж", "zh");
                        _words.Add("з", "z");
                        _words.Add("и", "i");
                        _words.Add("й", "j");
                        _words.Add("к", "k");
                        _words.Add("л", "l");
                        _words.Add("м", "m");
                        _words.Add("н", "n");
                        _words.Add("о", "o");
                        _words.Add("п", "p");
                        _words.Add("р", "r");
                        _words.Add("с", "s");
                        _words.Add("т", "t");
                        _words.Add("у", "u");
                        _words.Add("ф", "f");
                        _words.Add("х", "h");
                        _words.Add("ц", "c");
                        _words.Add("ч", "ch");
                        _words.Add("ш", "sh");
                        _words.Add("щ", "sch");
                        _words.Add("ъ", "j");
                        _words.Add("ы", "i");
                        _words.Add("ь", "j");
                        _words.Add("э", "e");
                        _words.Add("ю", "yu");
                        _words.Add("я", "ya");
                        _words.Add("А", "A");
                        _words.Add("Б", "B");
                        _words.Add("В", "V");
                        _words.Add("Г", "G");
                        _words.Add("Д", "D");
                        _words.Add("Е", "E");
                        _words.Add("Ё", "Yo");
                        _words.Add("Ж", "Zh");
                        _words.Add("З", "Z");
                        _words.Add("И", "I");
                        _words.Add("Й", "J");
                        _words.Add("К", "K");
                        _words.Add("Л", "L");
                        _words.Add("М", "M");
                        _words.Add("Н", "N");
                        _words.Add("О", "O");
                        _words.Add("П", "P");
                        _words.Add("Р", "R");
                        _words.Add("С", "S");
                        _words.Add("Т", "T");
                        _words.Add("У", "U");
                        _words.Add("Ф", "F");
                        _words.Add("Х", "H");
                        _words.Add("Ц", "C");
                        _words.Add("Ч", "Ch");
                        _words.Add("Ш", "Sh");
                        _words.Add("Щ", "Sch");
                        _words.Add("Ъ", "J");
                        _words.Add("Ы", "I");
                        _words.Add("Ь", "J");
                        _words.Add("Э", "E");
                        _words.Add("Ю", "Yu");
                        _words.Add("Я", "Ya");
                    }
                }

                return _words;
            }
        }

        public static string РусскийВТранслит(string value)
        {
            value = value.Trim();

            foreach (KeyValuePair<string, string> pair in words)
            {
                value = value.Replace(pair.Key, pair.Value);
            }

            return value;
        }
        #endregion

        // Из значения получаем byte[]
        public static byte[] GetBytes(object value)
        {
            if (value == null) return null;

            switch (value.GetType().Name)
            {
                case "String":
                case "Char":
                    return Encoding.UTF8.GetBytes(Convert.ToString(value));
                case "Boolean":
                    return BitConverter.GetBytes(Convert.ToBoolean(value));
                case "Int64":
                case "Int32":
                case "Int16":
                case "Double":
                case "Decimal":
                    {
                        try
                        {
                            using (var stream = new MemoryStream())
                            using (var writer = new BinaryWriter(stream))
                            {
                                writer.Write(Convert.ToDecimal(value));
                                return stream.ToArray();
                            }
                        }
                        catch { return null; }
                    }
                case "DateTime":
                    return BitConverter.GetBytes(Convert.ToDateTime(value).Ticks);
                default:
                    {
                        try
                        {
                            using (var stream = new MemoryStream())
                            {
                                var formatter = new BinaryFormatter();
                                formatter.Serialize(stream, value);
                                return stream.ToArray();
                            }
                        }
                        catch { return null; }
                    }
            }
        }

        // Из значения получаем byte[]
        public static object GetObject(Type type, byte[] value)
        {
            switch (type.Name)
            {
                case "String":
                case "Char":
                    return Encoding.UTF8.GetString(value);
                case "Boolean":
                    return BitConverter.ToBoolean(value, 0);
                case "Int64":
                case "Int32":
                case "Int16":
                case "Double":
                case "Decimal":
                    {
                        try
                        {
                            using (MemoryStream stream = new MemoryStream(value))
                            using (BinaryReader reader = new BinaryReader(stream))
                            {
                                return reader.ReadDecimal();
                            }
                        }
                        catch { return null; }
                    }
                case "DateTime":
                    return new DateTime(BitConverter.ToInt64(value, 0));
                default:
                    {
                        try
                        {
                            using (var stream = new System.IO.MemoryStream(value))
                            {
                                var formatter = new BinaryFormatter();
                                stream.Position = 0;
                                return formatter.Deserialize(stream);
                            }
                        }
                        catch { return null; }
                    }
            }
        }

        // Значения по умолчанию 
        public static object GetDefaultValue(Type type)
        {
            switch (type.Name)
            {
                case "String":
                case "Char":
                    return string.Empty;
                case "Boolean":
                    return false;
                case "Int64":
                case "Int32":
                case "Int16":
                case "Double":
                case "Decimal":
                    return 0M;
                default:
                    return null;
            }
        }
    }
}
