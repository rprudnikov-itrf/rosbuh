using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Data;
using RosService.DataClasses;
using System.Collections;
using RosService.Data;
using RosService.Configuration;

namespace RosService.Intreface
{
    [ServiceContract]
    interface IConfiguration
    {
        #region Основные функции
        [OperationContract]
        RosService.Configuration.Type ПолучитьТип(string Имя, string domain);

        [OperationContract]
        RosService.Configuration.Type[] СписокТипов(IEnumerable<string> СписокТиповДанных, string domain);
        [OperationContract]
        RosService.Configuration.Type[] СписокНаследуемыхТипов(string ТипДанных, bool ДобавитьБазовыйТип, string domain);

        [OperationContract]
        RosService.Configuration.Type[] СписокАтрибутов(string ТипДанных, string domain);

        [OperationContract]
        string[] СписокКатегорий(string domain);


        [OperationContract]
        object ПолучитьЗначение(string Тип, string Атрибут, string domain);

        [OperationContract]
        void СохранитьЗначение(string Тип, string attribute, object value, string domain);


        [OperationContract]
        void УдалитьТип(string ТипДанных, string domain);
        [OperationContract]
        void УдалитьАтрибут(string ТипДанных, string Атрибут, string domain);

        [OperationContract]
        bool ПроверитьНаследование(string БазовыйТип, string Тип, string user, string domain);
        #endregion

        #region DataBinder
        [OperationContract]
        Binding[] Binder_СписокСвязей(string ТипДанных, string domain);

        [OperationContract]
        void Binder_СохранитьСвязь(string ТипДанных, string attribute, string control, string PropertyPath, string StringFormat, string domain);

        [OperationContract]
        void Binder_УдалитьСвязи(string ТипДанных, string domain);
        #endregion

        #region Event
        [OperationContract]
        Event[] Event_СписокСобытий(string ТипДанных, string domain);

        [OperationContract]
        void Event_СохранитьСобытие(string ТипДанных, string control, string ИмяСобытия, string ИмяФункции, string domain);

        [OperationContract]
        void Event_УдалитьСобытие(string ТипДанных, string domain);
        #endregion

        #region Процессы
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        decimal Процесс_СоздатьПроцесс(string НазваниеПроцесса, string Описание, string Тип, string user, string domain);
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void Процесс_ОбновитьСостояниеПроцесса(decimal Процесс, double СостояниеПроцесса, string domain);
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void Процесс_ЗавершитьПроцесс(decimal Процесс, string domain);
        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        void Процесс_ОшибкаВПроцессе(decimal Процесс, string СообщениеОбОшибке, string domain);
        #endregion

        [OperationContract]
        Форма ПолучитьФорму(string ТипДанных, string domain);

        [OperationContract]
        void КопироватьТипДанных(string ТипДанных, string ИзДомена, string КопироватьВ, string КопироватьВДомен, УсловияКопирования УсловияКопирования, string user, string domain);


        [OperationContract]
        decimal Сервис_ДобавитьВебСервис(string Адрес, string Название, string user, string domain);
        [OperationContract]
        ВебСервис[] Сервис_СписокВебСервисов(string domain);



        [OperationContract]
        string ДобавитьТип(decimal Номер, string Имя, string Описание, string Категория, string БазовыйТип, bool IsМассив, bool ОбновитьКонфигурацию, string user, string domain);
        
        [OperationContract]
        void ДобавитьАтрибут(string ТипДанных, string Атрибут, bool ОбновитьКонфигурацию, string user, string domain);


        [OperationContract]
        string ОписаниеВИмя(string value);

        [OperationContract]
        void КомпилироватьКонфигурацию(string domain);

        [OperationContract]
        string[] СписокДоменов();

        [OperationContract(IsOneWay = true)]
        void ЖурналСобытийДобавитьОшибку(string Message, string StackTrace, string user, string domain);

        [OperationContract(IsOneWay = true)]
        void ОтправитьИнструкцию(decimal id_user, string user, string domain);

        [OperationContract]
        decimal ОтправитьПисьмоВТехническуюПоддержку(string ИмяДомена, string ОтКого, string ТемаСообщения, string ТекстСообщения, object СрокРеализации, bool Важно, Dictionary<string, byte[]> СписокФайлов, string user);

        [OperationContract]
        string ПолучитьIpАдресСоединения();


        #region Журналы / Отчеты / Справочники
        [OperationContract]
        Журнал[] СписокЖурналов(string user, string domain);

        [OperationContract]
        Отчет[] СписокОтчетов(string user, string domain);

        [OperationContract]
        Справочник[] СписокСправочников(string user, string domain);
        #endregion

        #region Авторизация
        [OperationContract]
        Пользователь Авторизация(string UserName, string Password, bool ЗаписатьВЖурнал, string domain);
        [OperationContract(IsOneWay = true)]
        void АвторизацияПродлитьСессиюПользователя(decimal СсылкаНаПользователя, TimeSpan ВремяСесси, string domain);

        [OperationContract(IsOneWay = true)]
        void АвторизацияБлокировки(bool Выключить);
        #endregion

        #region КешированныхОбъектов
        [OperationContract]
        void Ping();
        [OperationContract]
        IEnumerable<CacheObject> СписокКешированныхОбъектов(string user, string domain);
        [OperationContract]
        void УдалитьКешированныеОбъекты(string[] items, string user, string domain);
        [OperationContract]
        void УдалитьКешированныеЗначения(string user, string domain);
        #endregion

        [OperationContract]
        DeleteLog[] ЖурналУдалений(string domain);

        [OperationContract]
        void ComitTransaction();
    }
}
namespace RosService.Configuration
{
    #region Типы данных
    [Flags]
    public enum УсловияКопирования
    {
        НеОпределено = 0,
        Атрибуты = 1,
        Шаблон = 2,
        ИсходныйКод = 4,
        ЗначенияПоУмолчанию = 8,
        Иконка = 16,
        ВсеДомены = 32
    }

    /// <summary>
    /// Простейший тип
    /// </summary>
    [DataContract]
    public enum MemberTypes
    {
        [EnumMember]
        Undefined = 0,
        [EnumMember]
        Object = 1,
        [EnumMember]
        String = 2,
        [EnumMember]
        Int = 3,
        [EnumMember]
        Double = 4,
        [EnumMember]
        DateTime = 5,
        [EnumMember]
        Bool = 6,
        [EnumMember]
        Ссылка = 7,
        [EnumMember]
        Таблица = 8,
        [EnumMember]
        Byte = 9
    };
    /// <summary>
    /// Колонка в которой храниться значение
    /// </summary>
    [DataContract]
    public enum RegisterTypes
    {
        [EnumMember]
        undefined = 0,
        [EnumMember]
        double_value = 1,
        [EnumMember]
        datetime_value = 2,
        [EnumMember]
        string_value = 3,
        [EnumMember]
        byte_value = 4
    };

    [DataContract]
    [Serializable]
    public class Type : ICloneable
    {
        [DataMember]
        public string Описание { get; set; }
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string BaseType { get; set; }
        //тип который образуется при наследовании
        [DataMember]
        public string ReflectedType { get; set; }
        ////тип от которого начали просматривать класс
        [DataMember]
        public decimal DeclaringType { get; set; }


        //простейший тип
        [DataMember]
        public MemberTypes MemberType { get; set; }
        //регистр где храниться значения
        [DataMember]
        public RegisterTypes RegisterType { get; set; }

        [DataMember]
        public string Namespace { get; set; }
        [DataMember]
        public string HashCode { get; set; }
        [DataMember]
        public string TypeHashCode { get; set; }

        [DataMember]
        public bool IsReadOnly { get; set; }
        [DataMember]
        public bool IsAutoIncrement { get; set; }
        [DataMember]
        public bool IsSetDefaultValue { get; set; }

        public Type()
        {
        }
        public Type(assembly_tblAttribute t)
        {
            //id_node = t.id_node;
            //~d_type = t.~d_type;
            DeclaringType = t.id_parent;
            Name = t.Name;
            Описание = t.Описание;
            HashCode = t.HashCode;
            TypeHashCode = t.TypeHashCode;
            IsReadOnly = t.IsReadOnly;
            //IsSystem = t.IsSystem;
            IsAutoIncrement = t.IsAutoIncrement;
            IsSetDefaultValue = t.IsSetDefaultValue;
            Namespace = t.Namespace;
            MemberType = (MemberTypes)t.MemberType;
            RegisterType = (RegisterTypes)Enum.Parse(typeof(RegisterTypes), t.RegisterType);
            BaseType = t.BaseType;
            ReflectedType = t.ReflectedType;
        }
        public Type(DataRow t)
        {
            DeclaringType = t.Field<decimal>("id_parent");
            Name = t.Field<string>("Name");
            Описание = t.Field<string>("Описание");
            HashCode = t.Field<string>("HashCode");
            TypeHashCode = t.Field<string>("TypeHashCode");
            IsReadOnly = t.Field<bool>("IsReadOnly");
            //IsSystem = t.Field<bool>("IsSystem");
            IsAutoIncrement = t.Field<bool>("IsAutoIncrement");
            IsSetDefaultValue = t.Field<bool>("IsSetDefaultValue");
            Namespace = t.Field<string>("Namespace");
            MemberType = (MemberTypes)t.Field<int>("MemberType");
            RegisterType = (RegisterTypes)Enum.Parse(typeof(RegisterTypes), t.Field<string>("RegisterType"));
            BaseType = t.Field<string>("BaseType");
            ReflectedType = t.Field<string>("ReflectedType");
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    };
    [DataContract]
    [Serializable]
    public class Binding
    {
        [DataMember]
        public string attribute { get; set; }
        [DataMember]
        public string control { get; set; }
        [DataMember]
        public string PropertyPath { get; set; }
        [DataMember]
        public string StringFormat { get; set; }
        //[DataMember]
        //public string Valids { get; set; }
    };
    [DataContract]
    [Serializable]
    public class Event
    {
        [DataMember]
        public string control { get; set; }
        [DataMember]
        public string ИмяСобытия { get; set; }
        [DataMember]
        public string ИмяФункции { get; set; }
    };

    [DataContract]
    [Serializable]
    public class Форма
    {
        [DataMember]
        public string Xaml { get; set; }
        [DataMember]
        public string ИсходныйКод { get; set; }
        [DataMember]
        public Binding[] Bindings { get; set; }
        [DataMember]
        public Event[] Events { get; set; }
    };
    public class ВебСервис
    {
        public string Адрес;
        public string Название;
        public byte[] Файл;
        public string Namespace;
    }
    public class Журнал
    {
        public string Имя;
        public string Описание;
        public string Группа;
    };
    public class Отчет
    {
        public string Имя;
        public string Описание;
        public string Группа;
    };
    public class Справочник
    {
        public string Имя;
        public string Описание;
        public string Группа;
        public decimal id_node;
    };
    [DataContract]
    [Serializable]
    public class CacheObject
    {
        [DataMember]
        public string Type { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Value { get; set; }
        [DataMember]
        public string Content { get; set; }
        [DataMember]
        public int Count { get; set; }
        [DataMember]
        public uint? AvgTime { get; set; }
        [DataMember]
        public float PercentTime { get; set; }
        [DataMember]
        public float PercentCount { get; set; }
    }
    //[DataContract]
    //[Serializable]
    //public class PerformanceItem
    //{
    //    [DataMember]
    //    public string domain { get; set; }
    //    [DataMember]
    //    public long Count { get; set; }
    //    [DataMember]
    //    public long AvgTime { get; set; }
    //    [DataMember]
    //    public float PercentTime { get; set; }
    //    [DataMember]
    //    public float PercentCount { get; set; }
    //    [DataMember]
    //    public int CountQuery { get; set; }
    //    [DataMember]
    //    public float PercentCountQuery { get; set; }
    //    [DataMember]
    //    public long КешированноЗначений { get; set; }
    //    [DataMember]
    //    public double Оценка { get; set; }
    //}

    public class DeleteLog
    {
        public string  user { get; set; }
        public DateTime date { get; set; }
        public string type { get; set; }
        public string label { get; set; }
    }
    #endregion
}
