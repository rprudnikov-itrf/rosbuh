using System.Collections.Generic;
using System.ServiceModel;
using System.Xml;
using System.Linq;
using System.Collections;
using System.Data;
using System;
using RosService.Services;

namespace RosService.Intreface
{
    [ServiceContract]
    public interface IServices
    {
        #region Почта
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        long Почта_КоличествоПисем(decimal Пользователь, string domain);
        #endregion

        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void Данные_СписокТелефонов_Проиндекировать(decimal id_node, string user, string domain);

        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        DataTable Данные_СписокТелефонов_Совпадения(string Телефоны, string user, string domain);

        [OperationContract]
        string[] ПоискАдреса(string Адрес, int Количество);
        [OperationContract]
        БанкСведения[] ПоискБанка(string БИК);
        [OperationContract]
        string[] ПоискКоординатГеокодирование(string Адрес);

        [OperationContract(IsOneWay = true)]
        void СообщенияПользователя_Очистить(decimal СсылкаНаПользователя, decimal СсылкаНаОбъект, string user, string domain);
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void СообщенияПользователя_Добавить(object ОтКогоЛогинПользователя, object[] КомуЛогинПользователя, string Сообщение, string user, string domain);
        [OperationContract]
        IEnumerable<СообщенияПользователя> СообщенияПользователя_Список(decimal СсылкаНаПользователя, string user, string domain);

        [OperationContract]
        IEnumerable<СведенияПользователя> Пользователи_Список(string user, string domain);


        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void ЗадачаПользователя_Добавить(object ОтКогоЛогинПользователя, object[] КомуЛогинПользователя, string Сообщение, bool Срочно, object Срок, Dictionary<string, byte[]> Файлы, string user, string domain);
        [OperationContract]
        IEnumerable<ЗадачиПользователя> ЗадачаПользователя_Список(decimal СсылкаНаПользователя, string user, string domain);


        [OperationContract(IsOneWay = true)]
        void Рассылка(string Тема, string Содержание, object ПапкаАдресатами, Dictionary<string, byte[]> Файлы, string user, string domain);
        [OperationContract]
        Dictionary<string, object> СтатистикаКонфигурации(string user, string domain);
        [OperationContract(IsOneWay = true)]
        void ОптравитьВСтатистику(string Приложение, string Источник, DateTime Дата, decimal Значение);
    }
}
namespace RosService.Services
{
    public class БанкСведения
    {
        public string Название;
        public string БИК;
        public string Город;
        public string Адрес;
        public string КорСчет;
        //public string Филеал;
        //public string КПП;
    }
    public class СообщенияПользователя
    {
        public decimal id_node { get; set; }
        public int Количество { get; set; }
    };
    public class ЗадачиПользователя
    {
        public decimal id_node { get; set; }
        public int Новые { get; set; }
        public int Срочные { get; set; }
        public int Количество { get; set; }
    };
    public class СведенияПользователя
    {
        public decimal id_node { get; set; }
        public string НазваниеОбъекта { get; set; }
        public string Группа { get; set; }
        public bool ВСети { get; set; }
        public decimal? СсылкаНаАватар { get; set; }
    };
}

