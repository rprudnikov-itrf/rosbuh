using System.Collections.Generic;
using System.ServiceModel;
using System.Xml;
using System.Linq;
using System.Collections;
using System.Data;
using System;
using RosService.Finance;

namespace RosService.Intreface
{
    [ServiceContract]
    public interface IFinance
    {
        [OperationContract]
        decimal ДобавитьПлатеж(НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаДоговор, 
            string СтатьяДвиженияСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа, string СпособОплаты, string НазначениеПлатежа, 
            СтавкаНдс СтавкаНдс, string user, string domain);

        //[OperationContract]
        //IEnumerable<decimal> ДобавитьПлатежи(Платеж[] Платежи, string user, string domain);

        //[OperationContract]
        //decimal ДобавитьПлатежИзОжидаемых(decimal[] ОжидаемыеПлатежи, string СпособОплаты, string user, string domain);


        [OperationContract]
        decimal ДобавитьВнутреннийПеревод(НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаОбъект, string СтатьяДвиженияДенежныхСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа, string НазначениеПлатежа, string user, string domain);
        [OperationContract]
        decimal ДобавитьВнутреннийПереводНаСчет(string НазваниеСчета,
            НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаОбъект,
            string СтатьяДвиженияДенежныхСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа,
            string НазначениеПлатежа, string ТипДокумента, string user, string domain);



        //[OperationContract]
        //void ЛицевойСчетЗачислитьСписать(decimal СсылкаНаКонтрагента, double Сумма, string user, string domain);
        //[OperationContract]
        //void ПополнитьСписатьСчет(string НазваниеСчета, decimal СсылкаНаКонтрагента, double Сумма, string user, string domain);

        [OperationContract]
        void УдалитьВнутреннийПеревод(decimal Платеж, string user, string domain);

        [OperationContract]
        decimal ВыводСредств(decimal id_client, DateTime Дата, decimal Сумма, string СпособОплаты, string НазначениеПлатежа, bool УдерживатьНДФЛ, string user, string domain);
        [OperationContract]
        decimal ВыводСредствСоСчета(string НазваниеСчета, decimal СсылкаНаКонтрагента, DateTime Дата, decimal Сумма, string СпособОплаты, string НазначениеПлатежа, bool УдерживатьНДФЛ, string ТипДокумента, string user, string domain);


        [OperationContract]
        void ПеревестиСредства(decimal Отправитель, decimal Получатель, decimal Сумма, string Примечание, string user, string domain);
        [OperationContract]
        void ПеревестиСредстваСоСчета(string НазваниеСчета, decimal Отправитель, decimal Получатель, decimal Сумма, string Примечание, string ТипДокумента, string user, string domain);

        [OperationContract]
        decimal РегистрацияКлиента(decimal СсылкаНаМестоАгента, decimal РегиональныйПредставитель, string ВидКонтрагента, string СтатусКонтрагента, string Фамилия, string Имя, string Отчество, string Компания, string Почта, string Пароль, string user, string domain);

        #region верися 5.0
        [OperationContract]
        void ПополнитьСчет(string НазваниеСчета, decimal Получатель,
            decimal СсылкаНаОбъект, DateTime Дата, decimal Сумма,
            string Примечание, string user, string domain);

        [OperationContract]
        void СписатьСоСчета(string НазваниеСчета, decimal Получатель,
            decimal СсылкаНаОбъект, DateTime Дата, decimal Сумма,
            string Примечание, string user, string domain);

        [OperationContract]
        void ПеревестиСоСчета(string НазваниеСчета, 
            decimal Отправитель, decimal Получатель,
            decimal Сумма, string Примечание, string user, string domain);

        [OperationContract]
        void УдалитьСоСчета(string НазваниеСчета, decimal Получатель, string[] guid, string user, string domain);

        [OperationContract]
        void ОбновитьОстаткиНаСчетах(string user, string domain);
        #endregion


        [OperationContract]
        void ОбновитьСведенияКонтрагента(decimal Контрагент, string user, string domain);
        [OperationContract]
        void ОбновитьОстаткиНаВнутреннихСчетах(string user, string domain);

        [OperationContract]
        void ПроверитьБанковскиеРеквизитыКонтрагента(decimal Контрагент, string user, string domain);
        [OperationContract]
        void ПроверитьБанковскиеРеквизиты(string user, string domain);



        [OperationContract]
        //[TransactionFlow(TransactionFlowOption.Allowed)]
        decimal НДС(string СтавкаНдс);

        [OperationContract]
        string ЧислоПрописью(double Число, ФорматЧислаПрописью ФорматЧислаПрописью);

        [OperationContract(IsOneWay = true)]
        void ИнвентаризацияПродукцииНаСкладе(decimal[] Склады, DateTime ДатаИнвентаризации, string user, string domain);
        [OperationContract]
        IEnumerable<Инвентаризация> ИнвентаризацияПродукцииНаСкладеТаблица(decimal Склад, DateTime ДатаИнвентаризации, string user, string domain);
    }
}

namespace RosService.Finance
{
    public enum ФорматЧислаПрописью
    {
        Число,
        Рубли,
        Доллар,
        Евро,
        РублиБезКопеек
    }
    public enum ВидыДокумента
    {
        ПриходныйКассовыйОрдер,
        РасходныйКассовыйОрдер,
        ВходящееПлатежноеПоручение,
        ИсходящееПлатежноеПоручение,
        Операция
    }
    public enum НапралениеПлатежа
    {
        Приход,
        Расход
    }
    public enum СтавкаНдс
    {
        БезНДС,
        НДС0,
        НДС18,
        НДС18_118,
        НДС10,
        НДС10_110,
        НДС20,
        НДС20_120
    };
    public enum СтавкаНдфл
    {
        НДФЛ_13,
        НДФЛ_30
    };

    public class Платеж
    {
        public DateTime Дата;
        public string СтатьяДС;
        public string Описание;
        public decimal Договор;
        public decimal Контрагент;
        public decimal Сумма;
        public bool БезНал;
    }
    public class Инвентаризация
    {
        public decimal Товар { get; set; }
        public decimal КоличествоНаСкладе { get; set; }
        public decimal КоличествоПродано { get; set; }
        public decimal КоличествоОстаток { get; set; }
        public decimal СуммаПродано { get; set; }
    }
}

