using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;

namespace RosService.Configuration
{
    public partial class ConfigurationClient
    {
        public T ПолучитьЗначение<T>(string Имя, string attribute)
        {
            try
            {
                var value = ПолучитьЗначение(Имя, attribute, Client.Domain);
                if (value is T)
                {
                    return (T)value;
                }
                else
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
            catch (FormatException)
            {
                return default(T);
            }
            catch (OverflowException)
            {
                return default(T);
            }
            catch (ArgumentNullException)
            {
                return default(T);
            }
        }
        public void СохранитьЗначение(string Имя, string attribute, object value)
        {
            СохранитьЗначение(Имя, attribute, value, Client.Domain);
        }

        #region Функции
        public RosService.Configuration.Type ПолучитьТип(string Имя)
        {
            return ПолучитьТип(Имя, Client.Domain);
        }
        public RosService.Configuration.Type[] СписокТипов()
        {
            return СписокТипов(null, Client.Domain);
        }
        public RosService.Configuration.Type[] СписокТипов(string[] СписокТиповДанных)
        {
            return СписокТипов(СписокТиповДанных, Client.Domain);
        }
        public RosService.Configuration.Type[] СписокАтрибутов(string ТипДанных)
        {
            return СписокАтрибутов(ТипДанных, Client.Domain);
        }
        public void УдалитьТип(string ТипДанных)
        {
            УдалитьТип(ТипДанных, Client.Domain);
        }
        public void УдалитьАтрибут(string ТипДанных, string Атрибут)
        {
            УдалитьАтрибут(ТипДанных, Атрибут, Client.Domain);
        }
        public string[] СписокКатегорий()
        {
            return СписокКатегорий(Client.Domain);
        }
        #endregion

        public void ОтправитьПисьмоВТехническуюПоддержку(string ИмяДомена, string ОтКого, string ТемаСообщения, string ТекстСообщения, Dictionary<string, byte[]> СписокФайлов, string user)
        {
            ОтправитьПисьмоВТехническуюПоддержку(ИмяДомена, ОтКого, ТемаСообщения, ТекстСообщения, null, false, СписокФайлов, user);
        }

        public decimal Процесс_СоздатьПроцесс(string НазваниеПроцесса, string Описание)
        {
            return Процесс_СоздатьПроцесс(НазваниеПроцесса, Описание, string.Empty, Client.UserName, Client.Domain);
        }
        public decimal Процесс_СоздатьПроцесс(string НазваниеПроцесса, string Описание, string user, string domain)
        {
            return Процесс_СоздатьПроцесс(НазваниеПроцесса, Описание, string.Empty, user, domain);
        }

        public void ЖурналСобытийДобавитьОшибку(string Message, string StackTrace)
        {
            ЖурналСобытийДобавитьОшибку(Message, StackTrace, Client.UserName, Client.Domain);
        }
        public void ЖурналСобытийДобавитьОшибку(string Message)
        {
            ЖурналСобытийДобавитьОшибку(Message, string.Empty, Client.UserName, Client.Domain);
        }
    }
}
