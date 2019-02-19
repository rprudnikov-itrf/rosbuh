using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using RosService.Intreface;
using RosService.Helper;
using System.Transactions;
using System.Threading;
using RosService.Data;
using RosService.Configuration;
using System.Threading.Tasks;
using System.IO;


namespace RosService.Finance
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    AddressFilterMode = AddressFilterMode.Any,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false,
    ConfigurationName = "RosService.Finance")]
    public partial class FinanceClient : IFinance
    {
        public DataClient data = new DataClient();

        #region Кассовые платежи
        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        public decimal ДобавитьПлатеж(НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаДоговор,
            string СтатьяДвиженияСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа, string СпособОплаты,
            string НазначениеПлатежа, СтавкаНдс СтавкаНдс, string user, string domain)
        {
            if (ДатаПлатежа.Date == DateTime.Today) ДатаПлатежа = ДатаПлатежа.Date.Add(DateTime.Now.TimeOfDay);

            var ТипДокумента = string.Empty;
            switch (СпособОплаты.ToUpper())
            {
                case "НАЛИЧНЫМИ":
                case "НАЛИЧНЫЕ":
                case "НАЛ":
                    switch (НапралениеПлатежа)
                    {
                        case НапралениеПлатежа.Приход:
                            ТипДокумента = "ПриходныйКассовыйОрдер";
                            break;
                        case НапралениеПлатежа.Расход:
                            ТипДокумента = "РасходныйКассовыйОрдер";
                            break;
                    }
                    break;

                case "БЕЗНАЛИЧНЫЙ ПЕРЕВОД":
                case "БНАЛ":
                case "БЕЗНАЛ":
                    switch (НапралениеПлатежа)
                    {
                        case НапралениеПлатежа.Приход:
                            ТипДокумента = "ПлатежноеПоручениеВходящее";
                            break;
                        case НапралениеПлатежа.Расход:
                            ТипДокумента = "ПлатежноеПоручениеИсходящее";
                            break;
                    }
                    break;

                case "WEBMONEY.WMR":
                case "WEBMONEY.WME":
                case "WEBMONEY.WMZ":
                case "ЯНДЕКС.ДЕНЬГИ":
                case "ЭЛЕКТРОННЫЙ ПЛАТЕЖ":
                    {
                        ТипДокумента = "ЭлектронныйПлатеж";
                    }
                    break;

                default:
                    ТипДокумента = "ОперацияБухгалтерскийИНалоговыйУчет";
                    break;
                    //throw new Exception("Добавление платежа для данного способа оплаты не предусмотренно");
            }

            var values = new Dictionary<string, Value>();
            values.Add("СсылкаНаКонтрагента", new Value(СсылкаНаКонтрагента));
            values.Add("СсылкаНаДоговор", new Value(СсылкаНаДоговор));
            values.Add("ВидОперации", new Value("Оплата от покупателя"));
            values.Add("НаправлениеПлатежа", new Value(НапралениеПлатежа.ToString()));
            values.Add("ДатаПлатежа", new Value(ДатаПлатежа));
            values.Add("СпособОплаты", new Value(СпособОплаты.ToString()));
            values.Add("СтатьяДвиженияДенежныхСредств", new Value(СтатьяДвиженияСредств));
            values.Add("НазваниеОбъекта", new Value(НазначениеПлатежа));
            values.Add("СтатусПлатежа", new Value("Оплачен"));
            values.Add("СтавкаНдс", new Value(СтавкаНдс.ToString()));
            values.Add("СуммаНдс", new Value(СуммаПлатежа * НДС(СтавкаНдс.ToString())));
            values.Add("ОтражатьВНалоговомУчете", new Value(true));

            values.Add("СуммаПлатежа", new Value(СуммаПлатежа));
            switch (НапралениеПлатежа)
            {
                case НапралениеПлатежа.Приход:
                    values.Add("СуммаПриход", new Value(СуммаПлатежа));
                    break;
                case НапралениеПлатежа.Расход:
                    values.Add("СуммаРасход", new Value(СуммаПлатежа));
                    break;
            }
            return data.ДобавитьРаздел(СсылкаНаДоговор > 0 ? СсылкаНаДоговор : СсылкаНаКонтрагента, 
                ТипДокумента, values, true, Хранилище.Оперативное, user, domain);
        }

        ////[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        //public IEnumerable<decimal> ДобавитьПлатежи(Платеж[] Платежи, string user, string domain)
        //{
        //    //using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required))
        //    {
        //        var items = new List<decimal>(Платежи.Length);
        //        var dic = new Dictionary<string, DataRow>();
        //        var СтатьяДС = null as DataRow;
        //        foreach (var item in Платежи)
        //        {
        //            if (!dic.ContainsKey(item.СтатьяДС))
        //            {
        //                var query = new Query();
        //                query.Типы.Add("ЭлементСтатьяДвиженияСредств");
        //                query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "НазваниеОбъекта", Значение = item.СтатьяДС });
        //                query.ВыводимыеКолонки.Add(new Query.Колонка() { Атрибут = "*" });
        //                СтатьяДС = data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().SingleOrDefault();
        //                if (СтатьяДС == null)
        //                    throw new Exception(string.Format("Не найдена статья ДС '{0}'", item.СтатьяДС));
        //                dic.Add(item.СтатьяДС, СтатьяДС);
        //            }
        //            else
        //            {
        //                СтатьяДС = dic[item.СтатьяДС];
        //            }

        //            //поиск ссылки в договоре
        //            if (item.Контрагент == 0) 
        //                item.Контрагент = data.ПолучитьЗначение<decimal>(item.Договор, "СсылкаНаКонтрагента", Хранилище.Оперативное, domain);
        //            //установить ссылку как договор
        //            if (item.Контрагент == 0) 
        //                item.Контрагент = item.Договор;

        //            if (string.IsNullOrEmpty(СтатьяДС.Field<string>("НаправлениеПлатежа")))
        //                throw new Exception(string.Format("Не указано направление платежа для статьи ДС '{0}'", item.СтатьяДС));

        //            items.Add(ДобавитьПлатеж(
        //                (НапралениеПлатежа)Enum.Parse(typeof(НапралениеПлатежа), СтатьяДС.Field<string>("НаправлениеПлатежа")),
        //                item.Контрагент,
        //                item.Договор,
        //                item.СтатьяДС,
        //                item.Дата,
        //                item.Сумма,
        //                item.БезНал ? "Безналичный перевод" : "Наличные",
        //                item.Описание,
        //                СтавкаНдс.БезНДС,
        //                user,
        //                domain));
        //        }
        //        //scope.Complete();
        //        return items;
        //    }
        //}

        ////[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        //public decimal ДобавитьПлатежИзОжидаемых(decimal[] ОжидаемыеПлатежи, string СпособОплаты, string user, string domain)
        //{
        //    //проверка платежей
        //    if (ОжидаемыеПлатежи == null || ОжидаемыеПлатежи.Length == 0) 
        //        throw new Exception("Укажите хотя бы один платеж");


        //    var Платеж = 0m;
        //    var СсылкаНаКонтрагента = 0m;
        //    var Направление = null as object;
        //    var СсылкаНаДоговор = 0m;
        //    var СтатьяДС = "";

        //    var items = new List<Dictionary<string, Value>>();
        //    foreach (var item in ОжидаемыеПлатежи)
        //    {
        //        items.Add(data.ПолучитьЗначение(item, new string[] { "НазваниеОбъекта", "НаправлениеПлатежа", "СтатьяДвиженияДенежныхСредств", "СсылкаНаКонтрагента", "СсылкаНаДоговор", "СуммаПлатежа" }, Хранилище.Оперативное, domain));
        //    }
        //    Направление = items[0]["НаправлениеПлатежа"].Значение;
        //    СсылкаНаКонтрагента = Convert.ToDecimal(items[0]["СсылкаНаКонтрагента"].Значение);
        //    СсылкаНаДоговор = Convert.ToDecimal(items[0]["СсылкаНаДоговор"].Значение);
        //    СтатьяДС = Convert.ToString(items[0]["СтатьяДвиженияДенежныхСредств"].Значение);

        //    if (ОжидаемыеПлатежи.Length > 1)
        //    {
        //        if (items.Count(p => p["НаправлениеПлатежа"].Значение.Equals(Направление)) != ОжидаемыеПлатежи.Length)
        //            throw new Exception("Невозможно сгрупировать платежи Приход и Расход");
        //        if (items.Count(p => p["СсылкаНаДоговор"].Значение.Equals(СсылкаНаДоговор)) != ОжидаемыеПлатежи.Length)
        //            throw new Exception("Невозможно создать платеж для разных договоров");
        //        if (items.Count(p => p["СтатьяДвиженияДенежныхСредств"].Значение.Equals(СтатьяДС)) != ОжидаемыеПлатежи.Length)
        //            СтатьяДС = "";
        //    }

        //    //using (TransactionScope scope = new TransactionScope())
        //    {
        //        Платеж = ДобавитьПлатеж(
        //            (НапралениеПлатежа)Enum.Parse(typeof(НапралениеПлатежа), Convert.ToString(Направление)),
        //            СсылкаНаКонтрагента, //data.ПолучитьЗначение<decimal>(СсылкаНаДоговор, "СсылкаНаКонтрагента", Хранилище.Оперативное, domain),
        //            СсылкаНаДоговор,
        //            СтатьяДС,
        //            DateTime.Now,
        //            items.Sum(p => (decimal)p["СуммаПлатежа"].Значение),
        //            СпособОплаты,
        //            Convert.ToString(items[0]["НазваниеОбъекта"].Значение), 
        //            СтавкаНдс.БезНДС,
        //            user,
        //            domain);

        //        foreach (var item in ОжидаемыеПлатежи)
        //        {
        //            data.СохранитьЗначениеПростое(item, "СтатусПлатежа", "Оплачен", true, Хранилище.Оперативное, user, domain);
        //        }

        //        //scope.Complete();
        //    }
        //    return Платеж;
        //}
        #endregion

        #region Внутренний счёт
        public decimal ДобавитьВнутреннийПеревод(НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаОбъект,
            string СтатьяДвиженияДенежныхСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа,
            string НазначениеПлатежа, string user, string domain)
        {
            return ДобавитьВнутреннийПереводНаСчет("ВнутреннийСчет.ОстатокНаСчете", НапралениеПлатежа,
                СсылкаНаКонтрагента, СсылкаНаОбъект, СтатьяДвиженияДенежныхСредств,
                ДатаПлатежа, СуммаПлатежа, НазначениеПлатежа, null,
                user, domain);
        }
        public decimal ДобавитьВнутреннийПереводНаСчет(string НазваниеСчета,
            НапралениеПлатежа НапралениеПлатежа, decimal СсылкаНаКонтрагента, decimal СсылкаНаОбъект,
            string СтатьяДвиженияДенежныхСредств, DateTime ДатаПлатежа, decimal СуммаПлатежа,
            string НазначениеПлатежа, string ТипДокумента, string user, string domain)
        {
            if (string.IsNullOrEmpty(НазваниеСчета))
                throw new Exception("Название счета не может быть пустым.");

            if (ДатаПлатежа.Date == DateTime.Today)
                ДатаПлатежа = ДатаПлатежа.Date.Add(DateTime.Now.TimeOfDay);

            var id_node = 0m;
            var ОстатокНаСчете = data.ПолучитьЗначение<decimal>(СсылкаНаКонтрагента, НазваниеСчета, Хранилище.Оперативное, domain)
                + (НапралениеПлатежа == НапралениеПлатежа.Приход ? СуммаПлатежа : -СуммаПлатежа);


            //ПополнитьСписатьСчет(НазваниеСчета, СсылкаНаКонтрагента, НапралениеПлатежа == НапралениеПлатежа.Приход ? (double)СуммаПлатежа : -(double)СуммаПлатежа, user, domain);
            if (!string.IsNullOrEmpty(НазваниеСчета))
                new DataClient().СохранитьЗначениеПростое(СсылкаНаКонтрагента, НазваниеСчета, ОстатокНаСчете, false, Хранилище.Оперативное, user, domain);

            try
            {
                if (СсылкаНаОбъект == 0)
                    СсылкаНаОбъект = СсылкаНаКонтрагента;

                var values = new Dictionary<string, Value>();
                if (!string.IsNullOrEmpty(НазваниеСчета))
                    values.Add("НазваниеСчета", new Value(НазваниеСчета));
                values.Add("СпособОплаты", new Value("Внутренний перевод"));
                values.Add("СтатусПлатежа", new Value("Оплачен"));
                values.Add("НаправлениеПлатежа", new Value(НапралениеПлатежа.ToString()));
                values.Add("ДатаПлатежа", new Value(ДатаПлатежа));
                values.Add("СсылкаНаКонтрагента", new Value(СсылкаНаКонтрагента));
                if (СсылкаНаОбъект != 0)
                    values.Add("СсылкаНаДоговор", new Value(СсылкаНаОбъект));
                if (!string.IsNullOrEmpty(СтатьяДвиженияДенежныхСредств))
                    values.Add("СтатьяДвиженияДенежныхСредств", new Value(СтатьяДвиженияДенежныхСредств));
                values.Add("НазваниеОбъекта", new Value(НазначениеПлатежа));
                values.Add("СуммаПлатежа", new Value(СуммаПлатежа));
                switch (НапралениеПлатежа)
                {
                    case НапралениеПлатежа.Приход:
                        values.Add("СуммаПриход", new Value(СуммаПлатежа));
                        break;
                    case НапралениеПлатежа.Расход:
                        values.Add("СуммаРасход", new Value(СуммаПлатежа));
                        break;
                }
                values.Add("СуммаОстатокНаСчете", new Value(ОстатокНаСчете));

                if (string.IsNullOrEmpty(ТипДокумента))
                    ТипДокумента = "ВнутреннийПеревод";

                id_node = new DataClient().ДобавитьРаздел(СсылкаНаОбъект, ТипДокумента, values, false, Хранилище.Оперативное, user, domain);
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("ДобавитьВнутреннийПереводНаСчет", user, domain, ex.ToString());
                throw ex;
            }
            return id_node;
        }

        public decimal ВыводСредств(decimal id_client, DateTime Дата, decimal Сумма, string СпособОплаты, string НазначениеПлатежа, bool УдерживатьНДФЛ, string user, string domain)
        {
            return ВыводСредствСоСчета("ВнутреннийСчет.ОстатокНаСчете", id_client, Дата, Сумма, СпособОплаты, НазначениеПлатежа, УдерживатьНДФЛ, null, user, domain);
        }
        public decimal ВыводСредствСоСчета(string НазваниеСчета, decimal СсылкаНаКонтрагента, DateTime Дата, decimal Сумма, string СпособОплаты, string НазначениеПлатежа, 
            bool УдерживатьНДФЛ, string ТипДокумента, string user, string domain)
        {
            //var ОстатокНаСчете = data.ПолучитьЗначение<decimal>(id_client, "ВнутреннийСчет.ОстатокНаСчете", Хранилище.Оперативное, domain);
            //if (ОстатокНаСчете < Сумма) throw new Exception("Не достаточно средств на внутреннем счете");

            if (string.IsNullOrEmpty(НазваниеСчета))
                throw new Exception("Название счета не может быть пустым.");

            var Платеж = ДобавитьВнутреннийПереводНаСчет(НазваниеСчета,
                НапралениеПлатежа.Расход,
                СсылкаНаКонтрагента, СсылкаНаКонтрагента, "Вывод с внутреннего счета",
                Дата, Сумма, НазначениеПлатежа, ТипДокумента, user, domain);

            if (!string.IsNullOrEmpty(СпособОплаты) && СпособОплаты != "Внутренний перевод")
            {
                var НДФЛ = УдерживатьНДФЛ ? 0.13m : 0m;
                Платеж = ДобавитьПлатеж(
                            НапралениеПлатежа.Расход,
                            СсылкаНаКонтрагента, Платеж,
                            "Вывод с внутреннего счета",
                            Дата, Сумма * (1m - НДФЛ),
                            СпособОплаты,
                            НазначениеПлатежа, СтавкаНдс.БезНДС, user, domain);

                //if (НДФЛ > 0)
                //{
                //    var values = new Dictionary<string, Value>();
                //    values.Add("ДатаПлатежа", new Value(Дата));
                //    values.Add("НаправлениеПлатежа", new Value("Расход"));
                //    values.Add("СпособОплаты", new Value("Безналичный перевод"));
                //    values.Add("СсылкаНаДоговор", new Value(Платеж));
                //    values.Add("СсылкаНаКонтрагента", new Value(СсылкаНаКонтрагента));
                //    values.Add("СтатусПлатежа", new Value("Не оплачен"));
                //    values.Add("СтатьяДвиженияДенежныхСредств", new Value("Налог НДФЛ"));
                //    values.Add("СуммаПлатежа", new Value(Сумма * НДФЛ));
                //    values.Add("СтавкаНДФЛ", new Value("НДФЛ_13"));
                //    values.Add("НазваниеОбъекта", new Value("Оплата НДФЛ"));
                //    data.ДобавитьРаздел(Платеж, "ПлатежНдфл", values, false, Хранилище.Оперативное, user, domain);
                //}
            }
            return Платеж;
        }
        

        public void ПеревестиСредства(decimal Отправитель, decimal Получатель, decimal Сумма, string Примечание, string user, string domain)
        {
            ПеревестиСредстваСоСчета("ВнутреннийСчет.ОстатокНаСчете", Отправитель, Получатель, Сумма, Примечание, null, user, domain);
        }
        public void ПеревестиСредстваСоСчета(string НазваниеСчета, decimal Отправитель, decimal Получатель, decimal Сумма, string Примечание, string ТипДокумента, string user, string domain)
        {
            if (Отправитель == 0)
                throw new Exception("Не указан 'Отправитель'");
            if (Получатель == 0)
                throw new Exception("Не указан 'Получатель'");
            if (Сумма < 0)
                throw new Exception("Сумма должна быть больше нуля");

            Parallel.Invoke(
                delegate()
                {
                    ДобавитьВнутреннийПереводНаСчет(
                            НазваниеСчета,
                            НапралениеПлатежа.Расход,
                            Отправитель,
                            Получатель,
                            "Перевод с внутреннего счета",
                            DateTime.Now,
                            Сумма,
                            string.Format("Перевод для {0} {1}",
                                data.ПолучитьЗначение<string>(Получатель, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                                string.IsNullOrEmpty(Примечание) ? Примечание : "(" + Примечание + ")"),
                            ТипДокумента,
                            user,
                            domain);
                },
                delegate()
                {
                    ДобавитьВнутреннийПереводНаСчет(
                        НазваниеСчета,
                        НапралениеПлатежа.Приход,
                        Получатель,
                        Отправитель,
                        "Перевод с внутреннего счета",
                        DateTime.Now,
                        Сумма,
                        string.Format("Перевод от {0} {1}",
                        data.ПолучитьЗначение<string>(Отправитель, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                        string.IsNullOrEmpty(Примечание) ? Примечание : "(" + Примечание + ")"),
                        ТипДокумента,
                        user,
                        domain);
                }
            );
        }

        public void УдалитьВнутреннийПеревод(decimal Платеж, string user, string domain)
        {
            var values = data.ПолучитьЗначение(Платеж, new string[] { "НазваниеСчета", "Тип.Имя", "НаправлениеПлатежа", "СсылкаНаКонтрагента", "СуммаПлатежа", "ДатаСозданияОбъекта" }, Хранилище.Оперативное, domain);
            if (Convert.ToDouble(values["СуммаПлатежа"].Значение) == 0) return;

            var mod = values["НаправлениеПлатежа"].Значение.Equals("Приход") ? -1m : 1m;
            var НазваниеСчета = Convert.ToString(values["НазваниеСчета"].Значение);
            //ПополнитьСписатьСчет(
            //    !string.IsNullOrEmpty(НазваниеСчета) ? НазваниеСчета : "ВнутреннийСчет.ОстатокНаСчете",
            //    Convert.ToDecimal(values["СсылкаНаКонтрагента"].Значение),
            //    Convert.ToDouble(values["СуммаПлатежа"].Значение) * mod,
            //    user, domain);

            if (string.IsNullOrEmpty(НазваниеСчета))
                НазваниеСчета = "ВнутреннийСчет.ОстатокНаСчете";

            var ОстатокНаСчете = data.ПолучитьЗначение<decimal>(Convert.ToDecimal(values["СсылкаНаКонтрагента"].Значение), НазваниеСчета, Хранилище.Оперативное, domain);
            var Сумма = Convert.ToDecimal(values["СуммаПлатежа"].Значение) * mod;
            data.СохранитьЗначениеПростое(Convert.ToDecimal(values["СсылкаНаКонтрагента"].Значение),
                НазваниеСчета, ОстатокНаСчете + Сумма, true, Хранилище.Оперативное, user, domain);

            UpdatePays(
                Convert.ToDateTime(values["ДатаСозданияОбъекта"].Значение),
                Convert.ToDecimal(values["СсылкаНаКонтрагента"].Значение),
                НазваниеСчета,
                Сумма,
                user,
                domain);

        }

        private object lockPayObject = new System.Object();
        private void UpdatePays(DateTime date, decimal id_client, string account, decimal sum, string user, string domain)
        {
            //System.Threading.Tasks.Task.Factory.StartNew(() =>
            //{
            //    lock (lockPayObject)
            //    {
            //        try
            //        {
            //            var q = new Query();
            //            q.ДобавитьТипы("ВнутреннийПеревод");
            //            q.ДобавитьВыводимыеКолонки("СуммаОстатокНаСчете");
            //            q.ДобавитьУсловиеПоиска("СсылкаНаКонтрагента", id_client);
            //            q.ДобавитьУсловиеПоиска("НазваниеСчета", account);
            //            q.ДобавитьУсловиеПоиска("ДатаСозданияОбъекта", date, Query.Оператор.БольшеРавно).УчитыватьВремя = true;
            //            foreach (var item in client.Search(q).AsEnumerable())
            //            {
            //                client.SetAsync(item["id_node"], "СуммаОстатокНаСчете",
            //                    item.Convert<decimal>("СуммаОстатокНаСчете") + sum);
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            ConfigurationClient.WindowsLog(ex.Message, user, domain);
            //        }
            //    }
            //});
        }
        #endregion

        #region верися 5.0
        public void ПополнитьСчет(string НазваниеСчета, decimal Получатель,
            decimal СсылкаНаОбъект, DateTime Дата, decimal Сумма,
            string Примечание, string user, string domain)
        {
            var Ведомость = НазваниеСчета + ".Ведомость";
            var balance = ОбновитьОстаток(НазваниеСчета, Получатель, Сумма, user, domain);
            using (var ТаблицаВедомость = ПолучитьВедомость(Получатель, Ведомость, domain))
            {
                ТаблицаВедомость.Rows.Add(new object[] {
                    СсылкаНаОбъект,
                    Примечание,
                    DateTime.Now,
                    Сумма,
                    Convert.DBNull,
                    balance,
                    user,
                    Guid.NewGuid().ToString()
                });

                new DataClient().СохранитьЗначениеПростое(Получатель, Ведомость, DataClient.TableToXml(ТаблицаВедомость), false, Хранилище.Оперативное, user, domain);
            }
        }

        public void СписатьСоСчета(string НазваниеСчета, decimal Получатель,
            decimal СсылкаНаОбъект, DateTime Дата, decimal Сумма, 
            string Примечание, string user, string domain)
        {
            var Ведомость = НазваниеСчета + ".Ведомость";
            var balance = ОбновитьОстаток(НазваниеСчета, Получатель, -Сумма, user, domain);
            using (var ТаблицаВедомость = ПолучитьВедомость(Получатель, Ведомость, domain))
            {
                ТаблицаВедомость.Rows.Add(new object[] {
                    Получатель,
                    Примечание,
                    DateTime.Now,
                    Convert.DBNull,
                    Сумма,
                    balance,
                    user,
                    Guid.NewGuid().ToString()
                });

                new DataClient().СохранитьЗначениеПростое(Получатель, Ведомость, DataClient.TableToXml(ТаблицаВедомость), false, Хранилище.Оперативное, user, domain);
            }
        }

        public void ПеревестиСоСчета(string НазваниеСчета,
            decimal Отправитель, decimal Получатель, 
            decimal Сумма, string Примечание, string user, string domain)
        {
            var Ведомость = НазваниеСчета + ".Ведомость";
            var balance = ОбновитьОстаток(НазваниеСчета, Отправитель, -Сумма, user, domain);
            using (var ТаблицаВедомость = ПолучитьВедомость(Отправитель, Ведомость, domain))
            {
                ТаблицаВедомость.Rows.Add(new object[] {
                    Получатель,
                    string.Format("Перевод для {0} {1}",
                                data.ПолучитьЗначение<string>(Получатель, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                                string.IsNullOrEmpty(Примечание) ? Примечание : "(" + Примечание + ")"),
                    DateTime.Now,
                    Convert.DBNull,
                    Сумма,
                    balance,
                    user,
                    Guid.NewGuid().ToString()
                });
                new DataClient().СохранитьЗначениеПростое(Отправитель, Ведомость, DataClient.TableToXml(ТаблицаВедомость), false, Хранилище.Оперативное, user, domain);
            }

            balance = ОбновитьОстаток(НазваниеСчета, Получатель, Сумма, user, domain);
            using (var ТаблицаВедомость = ПолучитьВедомость(Получатель, Ведомость, domain))
            {
                ТаблицаВедомость.Rows.Add(new object[] {
                    Отправитель,
                    string.Format("Перевод от {0} {1}",
                                data.ПолучитьЗначение<string>(Отправитель, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                                string.IsNullOrEmpty(Примечание) ? Примечание : "(" + Примечание + ")"),
                    DateTime.Now,
                    Сумма,
                    Convert.DBNull,
                    balance,
                    user,
                    Guid.NewGuid().ToString()
                });
                new DataClient().СохранитьЗначениеПростое(Получатель, Ведомость, DataClient.TableToXml(ТаблицаВедомость), false, Хранилище.Оперативное, user, domain);
            }
        }

        public void УдалитьСоСчета(string НазваниеСчета, decimal Получатель, string[] guid, string user, string domain)
        {
            if (guid.Length == 0) 
                return;

            var Ведомость = НазваниеСчета + ".Ведомость";
            var Сумма = 0m;
            using (var ТаблицаВедомость = ПолучитьВедомость(Получатель, Ведомость, domain))
            {
                foreach (var g in guid)
                {
                    var guidNode = null as DataRow;
                    foreach (var item in ТаблицаВедомость.AsEnumerable())
                    {
                        if (guidNode == null)
                        {
                            if (g.Equals(item["guid"]))
                            {
                                guidNode = item;
                                Сумма = !Convert.IsDBNull(item["Приход"]) ? item.Convert<decimal>("Приход") : -item.Convert<decimal>("Расход");
                            }
                        }
                        else
                        {
                            item["Остаток"] = item.Convert<decimal>("Остаток") - Сумма;
                        }
                    }
                    ОбновитьОстаток(НазваниеСчета, Получатель, -Сумма, user, domain);
                    ТаблицаВедомость.Rows.Remove(guidNode);
                    new DataClient().СохранитьЗначениеПростое(Получатель, Ведомость, DataClient.TableToXml(ТаблицаВедомость), false, Хранилище.Оперативное, user, domain);
                }
            }
        }

        public void ОбновитьОстаткиНаСчетах(string user, string domain)
        {
            var configuration = new ConfigurationClient();
            decimal Процесс = configuration.Процесс_СоздатьПроцесс("Контрагенты", "Обновление остатков на счетах", "Процесс", user, domain);

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var query = new RosService.Data.Query();
                    query.ДобавитьТипы("ЭлементВнутреннийСчет");
                    query.ДобавитьВыводимыеКолонки("ИмяТипаДанных");
                    query.ДобавитьМестоПоиска("ВнутренниеСчета", 1);
                    var accounts = new DataClient().Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                        .Select(p => new Account() { AccountName = p.Field<string>("ИмяТипаДанных"), Cond = p.Field<string>("ИмяТипаДанных") });

                    var page = 0;
                    var oRow = 0d; 
                    while (true)
                    {
                        query = new RosService.Data.Query();
                        query.КоличествоВыводимыхДанных = 5000;
                        query.Страница = page;
                        query.ДобавитьТипы("Контрагент%");
                        foreach (var ac in accounts)
                            query.ДобавитьВыводимыеКолонки(ac.AccountName);
                        var clients = new DataClient().Поиск(query, Хранилище.Оперативное, domain);
                        foreach (var ac in accounts)
                        {
                            //var option = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount / 2 > 0 ? Environment.ProcessorCount / 2 : 1 };
                            //Parallel.ForEach(clients.AsEnumerable(), option, item =>
                            foreach (var item in clients.AsEnumerable())
                            {
                                var attr = ac.AccountName + ".Ведомость";
                                var table = ПолучитьВедомость(item.Field<decimal>("id_node"), attr, domain);
                                var sum = table.AsEnumerable().Sum(p => p.Field<decimal>("Приход")) - table.AsEnumerable().Sum(p => p.Field<decimal>("Расход"));
                                data.СохранитьЗначениеПростое(item.Field<decimal>("id_node"), attr, sum, false, Хранилище.Оперативное, user, domain);

                                if (oRow++ % 100 == 0)
                                    configuration.Процесс_ОбновитьСостояниеПроцесса(Процесс, oRow, domain);
                            }//);
                        }

                        if (++page > clients.PageCount)
                            break;
                    }

                    configuration.Процесс_ЗавершитьПроцесс(Процесс, domain);
                    try { new Services.ServicesClient().СообщенияПользователя_Добавить("ЛокальнаяСистема", new object[] { user }, "Процесс обновления остатков завершён", user, domain); }
                    catch { }
                }
                catch (Exception ex)
                {
                    configuration.Процесс_ОшибкаВПроцессе(Процесс, ex.Message, domain);
                }
            });
        }

        private DataTable ПолучитьВедомость(decimal Получатель, string Ведомость, string domain)
        {
            var stream = new DataClient().ПолучитьЗначение<string>(Получатель, Ведомость, Хранилище.Оперативное, domain);
            var ТаблицаВедомость = new System.Data.DataTable("r");

            //создать таблицу
            if (string.IsNullOrEmpty(stream))
            {
                ТаблицаВедомость.Columns.Add("id", typeof(decimal));
                ТаблицаВедомость.Columns.Add("НазваниеОбъекта", typeof(string));
                ТаблицаВедомость.Columns.Add("Дата", typeof(DateTime));
                ТаблицаВедомость.Columns.Add("Приход", typeof(decimal)).AllowDBNull = true;
                ТаблицаВедомость.Columns.Add("Расход", typeof(decimal)).AllowDBNull = true;
                ТаблицаВедомость.Columns.Add("Остаток", typeof(decimal));
                ТаблицаВедомость.Columns.Add("Пользователь", typeof(string));
                ТаблицаВедомость.Columns.Add("guid", typeof(string));
            }
            else
            {
                ТаблицаВедомость.ReadXml(new StringReader(stream));
                if (ТаблицаВедомость.Columns["guid"] == null)
                    ТаблицаВедомость.Columns.Add("guid", typeof(string));
            }
            return ТаблицаВедомость;
        }
        private decimal ОбновитьОстаток(string НазваниеСчета, decimal Получатель, decimal Сумма, string user, string domain)
        {
            var Остаток = new DataClient().ПолучитьЗначение<decimal>(Получатель, НазваниеСчета, Хранилище.Оперативное, domain) + Сумма;
            new DataClient().СохранитьЗначениеПростое(Получатель, НазваниеСчета, Остаток, false, Хранилище.Оперативное, user, domain);
            return Остаток;
        }
        #endregion

        #region Контрагенты
        public decimal РегистрацияКлиента(decimal СсылкаНаМестоАгента, decimal РегиональныйПредставитель,
            string ВидКонтрагента, string СтатусКонтрагента,
            string Фамилия, string Имя, string Отчество,
            string Компания, string Почта, string Пароль,
            string user, string domain)
        {
            var агент = data.ПолучитьРаздел(СсылкаНаМестоАгента, null, Хранилище.Оперативное, domain);
            if (агент == null) throw new Exception("Агент не найден.");

            var values = new Dictionary<string, Value>();
            values.Add("ЮридическоеЛицо.НазваниеОрганизации", new Value(Компания));
            values.Add("ФизическоеЛицо.Фамилия", new Value(Фамилия));
            values.Add("ФизическоеЛицо.Имя", new Value(Имя));
            values.Add("ФизическоеЛицо.Отчество", new Value(Отчество));
            values.Add("СсылкаНаРегиональногоПредставителя", new Value(РегиональныйПредставитель));
            values.Add("ВидКонтрагента", new Value(ВидКонтрагента));
            values.Add("ПарольПользователя", new Value(Пароль));
            values.Add("Контакты.Email", new Value(Почта));
            values.Add("СтатусКонтрагента", new Value(СтатусКонтрагента));
            values.Add("СсылкаНаРуководителя", new Value(СсылкаНаМестоАгента));
            var НазваниеОбъекта = "";
            switch (ВидКонтрагента)
            {
                case "Юридическое лицо":
                    НазваниеОбъекта = Компания;
                    break;
                default:
                    НазваниеОбъекта = string.Format("{0} {1} {2}", Фамилия, Имя, Отчество);
                    break;
            }
            values.Add("НазваниеОбъекта", new Value(НазваниеОбъекта));
            var Клиент = data.ДобавитьРаздел(14010, "Контрагент", values, true, Хранилище.Оперативное, user, domain);
            var Место = data.ДобавитьРаздел(СсылкаНаМестоАгента, "МестоВСтруктуре", null, true, Хранилище.Оперативное, user, domain);
            data.СохранитьЗначениеПростое(Место, "СсылкаНаКонтрагента", Клиент, false, Хранилище.Оперативное, user, domain);
            data.СохранитьЗначениеПростое(Место, "НазваниеОбъекта", НазваниеОбъекта, false, Хранилище.Оперативное, user, domain);
            //ПроверитьБанковскиеРеквизитыКонтрагента(Клиент, user, domain);
            return Место;
        }


        public void ОбновитьСведенияКонтрагента(decimal Контрагент, string user, string domain)
        {
            //using (TransactionScope scope = new TransactionScope())
            {
                ПроверитьБанковскиеРеквизитыКонтрагента(Контрагент, user, domain);

                var НазваниеОбъекта = "";
                switch (data.ПолучитьЗначение<string>(Контрагент, "ВидКонтрагента", Хранилище.Оперативное, domain))
                {
                    case "Юридическое лицо":
                        НазваниеОбъекта = data.ПолучитьЗначение<string>(Контрагент, "ЮридическоеЛицо.НазваниеОрганизации", Хранилище.Оперативное, domain);
                        break;
                    default:
                        НазваниеОбъекта = string.Format("{0} {1} {2}",
                            data.ПолучитьЗначение<string>(Контрагент, "ФизическоеЛицо.Фамилия", Хранилище.Оперативное, domain),
                            data.ПолучитьЗначение<string>(Контрагент, "ФизическоеЛицо.Имя", Хранилище.Оперативное, domain),
                            data.ПолучитьЗначение<string>(Контрагент, "ФизическоеЛицо.Отчество", Хранилище.Оперативное, domain));
                        break;
                }
                data.СохранитьЗначениеПростое(Контрагент, "НазваниеОбъекта", НазваниеОбъекта, false, Хранилище.Оперативное, user, domain);


                //переименовать структурные места
                try
                {
                    var query = new Query();
                    query.Типы.Add("МестоВСтруктуре%");
                    query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "СсылкаНаКонтрагента", Значение = Контрагент, Оператор = Query.Оператор.Равно });
                    foreach (var item in data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable())
                    {
                        data.СохранитьЗначениеПростое(item.Field<decimal>("id_node"), "НазваниеОбъекта", НазваниеОбъекта, false, Хранилище.Оперативное, user, domain);
                    }
                }
                catch
                {
                }
                //scope.Complete();
            }
        }
        #endregion

        #region Работа с банком
        public void ПроверитьБанковскиеРеквизиты(string user, string domain)
        {
            var configuration = new ConfigurationClient();
            decimal Процесс = configuration.Процесс_СоздатьПроцесс("Контрагенты", "Проверка банковских реквизитов", "Процесс", user, domain);

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var query = new Query();
                    query.Типы.Add("Контрагент%");

                    var row = 0d;
                    var items = data.Поиск(query, Хранилище.Оперативное, domain).Значение;
                    foreach (var item in items.AsEnumerable())
                    {
                        //using (var scope = new TransactionScope())
                        {
                            ПроверитьБанковскиеРеквизитыКонтрагента(item.Field<decimal>("id_node"), user, domain);
                            //scope.Complete();
                        }
                        configuration.Процесс_ОбновитьСостояниеПроцесса(Процесс, row / (double)items.Rows.Count * 100, domain);
                    }
                    configuration.Процесс_ЗавершитьПроцесс(Процесс, domain);
                }
                catch (Exception ex)
                {
                    configuration.Процесс_ОшибкаВПроцессе(Процесс, ex.Message, domain);
                }
            });
        }

        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        public void ПроверитьБанковскиеРеквизитыКонтрагента(decimal Контрагент, string user, string domain)
        {
            var trace = new List<string>();
            var СпособВзаиморасчетов = data.ПолучитьЗначение<string>(Контрагент, "СпособВзаиморасчетов", Хранилище.Оперативное, domain);
            switch(СпособВзаиморасчетов)
            {
                case "Безналичный перевод":
                    {
                        var values = data.ПолучитьЗначение(Контрагент,
                            new string[] 
                            { 
                                "БанковскиеРеквизиты.БикБанка",
                                "БанковскиеРеквизиты.КорреспондентскийСчет",
                                "БанковскиеРеквизиты.НомерКартыСберкнижки",
                                "БанковскиеРеквизиты.НомерСчетаКлиента",
                                "БанковскиеРеквизиты.ПолноеНазваниеБанка",
                                "БанковскиеРеквизиты.РасчётныйСчетБанка",
                                "БанковскиеРеквизиты.ТипБанковскогоСчёта",
                                "НалоговыеРеквизиты.ИНН"
                            },
                            Хранилище.Оперативное, domain);

                        var ТипБанковскогоСчёта = Convert.ToString(values["БанковскиеРеквизиты.ТипБанковскогоСчёта"].Значение);
                        if (string.IsNullOrEmpty(ТипБанковскогоСчёта))
                        {
                            trace.Add("Не указан тип банковского счёта");
                        }
                        if (string.IsNullOrEmpty(Convert.ToString(values["БанковскиеРеквизиты.НомерСчетаКлиента"].Значение)))
                        {
                            trace.Add("Не указан лицевой счёт");
                        }
                        if (string.IsNullOrEmpty(Convert.ToString(values["БанковскиеРеквизиты.БикБанка"].Значение)))
                        {
                            trace.Add("Не указан БИК банка");
                        }
                        if (Convert.ToString(values["БанковскиеРеквизиты.КорреспондентскийСчет"].Значение).Length < 20)
                        {
                            trace.Add("Не правильно указан кореспондентский счёт банка");
                        }
                        if (string.IsNullOrEmpty(Convert.ToString(values["НалоговыеРеквизиты.ИНН"].Значение)) || (Convert.ToString(values["НалоговыеРеквизиты.ИНН"].Значение).Length > 12 || Convert.ToString(values["НалоговыеРеквизиты.ИНН"].Значение).Length < 8))
                        {
                            trace.Add("Не правильно указан ИНН клиента (только цифры, не менее 8)");
                        }


                        switch (ТипБанковскогоСчёта)
                        {
                            case "Прямой лицевой счет":
                                {
                                    if (!(Convert.ToString(values["БанковскиеРеквизиты.НомерСчетаКлиента"].Значение).Length == 20 || Convert.ToString(values["БанковскиеРеквизиты.НомерСчетаКлиента"].Значение).Length == 23))
                                    {
                                        trace.Add("В лицевом счёте должено быть 20 или 23 цифры");
                                    }
                                }
                                break;

                            case "Сберкнижка (не пенсионная)":
                                {
                                    if (string.IsNullOrEmpty(Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение)))
                                    {
                                        trace.Add("Не указан расчетный счёт");
                                    }
                                    else if (!(Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение).Length == 20 || Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение).Length == 23))
                                    {
                                        trace.Add("В расчетном счёте должено быть 20 или 23 цифры");
                                    }
                                }
                                break;

                            case "Пластиковая карта (не заработная плата)":
                                {
                                    if (string.IsNullOrEmpty(Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение)))
                                    {
                                        trace.Add("Не указан расчетный счёт");
                                    }
                                    else if (!(Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение).Length == 20 || Convert.ToString(values["БанковскиеРеквизиты.РасчётныйСчетБанка"].Значение).Length == 23))
                                    {
                                        trace.Add("В расчетном счёте должено быть 20 или 23 цифры");
                                    }


                                    if (string.IsNullOrEmpty(Convert.ToString(values["БанковскиеРеквизиты.НомерКартыСберкнижки"].Значение)))
                                    {
                                        trace.Add("Не указан номер пластиковой карты");
                                    }
                                    else if (!(Convert.ToString(values["БанковскиеРеквизиты.НомерКартыСберкнижки"].Значение).Length >= 5 && Convert.ToString(values["БанковскиеРеквизиты.НомерКартыСберкнижки"].Значение).Length <= 26))
                                    {
                                        trace.Add("В номере пл. карты должено быть 5-26 цифр");
                                    }
                                }
                                break;
                        }
                    }
                    break;

                case "Наличными":
                case "Наличные":
                    break;

                default:
                    trace.Add("Не указан способ взаиморсчетов с контрагентом");
                    break;
            }
            data.СохранитьЗначениеПростое(Контрагент, "ОшибкиПриЗаполненииРеквизитов", string.Join("; ", trace.ToArray()), false, Хранилище.Оперативное, user, domain);
        }

        //[OperationBehavior(TransactionAutoComplete = true, TransactionScopeRequired = true)]
        private class Account
        {
            public string AccountName { get; set; }
            public string Cond { get; set; }
        };
        public void ОбновитьОстаткиНаВнутреннихСчетах(string user, string domain)
        {

            var configuration = new ConfigurationClient();
            decimal Процесс = configuration.Процесс_СоздатьПроцесс("Контрагенты", "Обновление остатков на счетах", "Процесс", user, domain);

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    //заблокировать переводы
                    try
                    {
                        data.СохранитьЗначениеПростое("ВнутренниеСчета.Заблокировать", "ЗначениеКонстанты", true, false, Хранилище.Оперативное, user, domain);
                    }
                    catch
                    {
                        var v = new Dictionary<string, Value>();
                        v.Add("ЗначениеКонстанты", new Value(true));
                        v.Add("ИдентификаторОбъекта", new Value("ВнутренниеСчета.Заблокировать"));
                        v.Add("НазваниеОбъекта", new Value("ВнутренниеСчета.Заблокировать"));
                        data.ДобавитьРаздел("Константы", "Константа", v, false, Хранилище.Оперативное, user, domain);
                    }

                    var accounts = null as IEnumerable<Account>;
                    try
                    {
                        var query = new RosService.Data.Query();
                        query.ДобавитьТипы("ЭлементВнутреннийСчет");
                        query.ДобавитьВыводимыеКолонки("ИмяТипаДанных");
                        query.ДобавитьМестоПоиска("ВнутренниеСчета", 1);
                        accounts = new DataClient().Поиск(query, Хранилище.Оперативное, domain).AsEnumerable()
                            .Select(p => new Account() { AccountName = p.Field<string>("ИмяТипаДанных"), Cond = p.Field<string>("ИмяТипаДанных") }); 
                    }
                    catch
                    {
                        //accounts = new Account[] { new Account() { AccountName = "ВнутреннийСчет.ОстатокНаСчете" } };
                    }

                    if (accounts == null || (accounts.Count() > 0 && string.IsNullOrEmpty(accounts.FirstOrDefault().AccountName)))
                    {
                        accounts = new Account[] { new Account() { AccountName = "ВнутреннийСчет.ОстатокНаСчете" } };
                    }

                    var oAccount = 0;
                    foreach (var ac in accounts)
                    {
                        var query = new RosService.Data.Query();
                        query.ДобавитьТипы("ВнутреннийПеревод%");
                        if (!string.IsNullOrEmpty(ac.Cond))
                        {
                            query.ДобавитьУсловиеПоиска("НазваниеСчета", ac.Cond);
                        }
                        query.ДобавитьВыводимыеКолонки("НаправлениеПлатежа", "СсылкаНаКонтрагента", "СуммаПлатежа");
                        query.ДобавитьСортировку("НазваниеОбъекта");
                        var pays = new DataClient().Поиск(query, Хранилище.Оперативное, domain).AsEnumerable();

                        var oRow = 0d;
                        var counts = pays.Count();
                        var step = Math.Round(Convert.ToDouble(counts) * 0.01, 0);

                        //var option = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount / 2 > 0 ? Environment.ProcessorCount / 2 : 1 };
                        foreach (var item in pays.GroupBy(p => p.Field<decimal>("СсылкаНаКонтрагента")))
                        {
                            var Сумма = item.Sum(p => p.Field<string>("НаправлениеПлатежа") == "Расход" ? -p.Field<decimal>("СуммаПлатежа") : p.Field<decimal>("СуммаПлатежа"));
                            Сумма = item.Where(p => "Приход".Equals(p.Field<string>("НаправлениеПлатежа"))).Sum(p => p.Field<decimal>("СуммаПлатежа"))
                                - item.Where(p => "Расход".Equals(p.Field<string>("НаправлениеПлатежа"))).Sum(p => p.Field<decimal>("СуммаПлатежа"));
                            data.СохранитьЗначениеПростое(item.Key, ac.AccountName, Сумма, false, Хранилище.Оперативное, user, domain);

                            if (oRow++ % step == 0)
                            {
                                configuration.Процесс_ОбновитьСостояниеПроцесса(Процесс, ((double)oRow) / ((double)counts) * 100 + (oAccount * 100), domain);
                            }
                        }

                        oAccount++;
                    }

                    configuration.Процесс_ЗавершитьПроцесс(Процесс, domain);
                    data.СохранитьЗначениеПростое("ВнутренниеСчета.Заблокировать", "ЗначениеКонстанты", false, false, Хранилище.Оперативное, user, domain);

                    try{ new Services.ServicesClient().СообщенияПользователя_Добавить("ЛокальнаяСистема", new object[] { user }, "Процесс обновления остатков завершён", user, domain); }
                    catch { }
                }
                catch (Exception ex)
                {
                    configuration.Процесс_ОшибкаВПроцессе(Процесс, ex.Message, domain);
                }
            });
        }
        #endregion

        #region Справочники
        public string ЧислоПрописью(double Число, ФорматЧислаПрописью ФорматЧислаПрописью)
        {
            switch (ФорматЧислаПрописью)
            {
                case ФорматЧислаПрописью.Число:
                    return NumberToStringConverter.Convert(Convert.ToInt64(Число)).Trim();

                case ФорматЧислаПрописью.Рубли:
                    return CurrencySumsConverter.RublesConverter.Convert(Convert.ToDecimal(Число));

                case ФорматЧислаПрописью.Доллар:
                    return CurrencySumsConverter.DollarsConverter.Convert(Convert.ToDecimal(Число));

                case ФорматЧислаПрописью.Евро:
                    return CurrencySumsConverter.EuroConverter.Convert(Convert.ToDecimal(Число));

                case ФорматЧислаПрописью.РублиБезКопеек:
                    return CurrencySumsConverter.RublesConverter.ConvertShort(Convert.ToDecimal(Число));

                default:
                    return Число.ToString();
            }
        }
        public decimal НДС(string СтавкаНдс)
        {
            switch (СтавкаНдс)
            {
                case "НДС18":
                    return 0.18M;
                case "НДС18_118":
                    return 18M / 118M;

                case "НДС10":
                    return 0.10M;
                case "НДС10_110":
                    return 10M / 110M;

                case "НДС20":
                    return 0.20M;
                case "НДС20_120":
                    return 20M / 120M;

                case "НДС0":
                case "БезНДС":
                default:
                    return 0;
            }
        }
        #endregion

        #region Склад
        public void ИнвентаризацияПродукцииНаСкладе(decimal[] Склады, DateTime ДатаИнвентаризации, string user, string domain)
        {
            try
            {
                if (ДатаИнвентаризации.Date == DateTime.Today)
                    ДатаИнвентаризации = DateTime.Now;

                foreach (var item in Склады)
                {
                    var values = new Dictionary<string, Value>();
                    values.Add("ДатаЗаключенияДоговора", new Value(ДатаИнвентаризации));
                    values.Add("СсылкаНаСклад", new Value(item));
                    var Документ = data.ДобавитьРаздел(item, "ИнвентаризацияТоваровНаСкладе", values, false, Хранилище.Оперативное, user, domain);

                    var tbl = ИнвентаризацияПродукцииНаСкладеТаблица(item, ДатаИнвентаризации, user, domain);
                    foreach (var row in tbl)
                    {
                        values = new Dictionary<string, Value>();
                        values.Add("КоличествоНаСкладе", new Value(row.КоличествоНаСкладе));
                        values.Add("КоличествоОстаток", new Value(row.КоличествоОстаток));
                        values.Add("КоличествоПродано", new Value(row.КоличествоПродано));
                        values.Add("Сумма", new Value(row.СуммаПродано));
                        values.Add("СсылкаНаНоменклатуру", new Value(row.Товар));
                        data.ДобавитьРаздел(Документ, "ЭлементИнвентаризации", values, false, Хранилище.Оперативное, user, domain);
                    }
                    
                    values = new Dictionary<string, Value>();
                    values.Add("КоличествоНаСкладе", new Value(tbl.Sum(p => p.КоличествоНаСкладе)));
                    values.Add("КоличествоОстаток", new Value(tbl.Sum(p => p.КоличествоОстаток)));
                    values.Add("КоличествоПродано", new Value(tbl.Sum(p => p.КоличествоПродано)));
                    data.СохранитьЗначение(Документ, values, false, Хранилище.Оперативное, user, domain);
                }
            }
            catch(Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку(ex.Message, ex.ToString(), user, domain);
            }
        }
        public IEnumerable<Инвентаризация> ИнвентаризацияПродукцииНаСкладеТаблица(decimal Склад, DateTime ДатаИнвентаризации, string user, string domain)
        {
            var query = new Query();
            query.ДобавитьТипы("ЭлементТовар%", "РеализацияТоваровИУслуг%");
            query.ДобавитьВыводимыеКолонки("СсылкаНаНоменклатуру");
            query.ДобавитьГруппировки("СсылкаНаНоменклатуру");
            query.ДобавитьУсловиеПоиска("@РодительскийРаздел/СсылкаНаСклад", Склад);
            query.ДобавитьУсловиеПоиска("@РодительскийРаздел/ДатаОплатыДоговора", ДатаИнвентаризации, Query.Оператор.МеньшеРавно);
            query.ДобавитьВыводимыеКолонки("Количество", Query.ФункцияАгрегации.Sum);
            query.ДобавитьВыводимыеКолонки("Сумма", Query.ФункцияАгрегации.Sum);
            query.ДобавитьВычисляемыеКолонки("sum(case when types.[value] = 'ЭлементТовар' then Количество.value else 0 end) 'НаСкладе'");
            query.ДобавитьВычисляемыеКолонки("sum(case when types.[value] = 'РеализацияТоваровИУслуг' then Количество.value else 0 end) 'Продано'");
            query.ДобавитьВычисляемыеКолонки("sum(case when types.[value] = 'ЭлементТовар' then Количество.value else 0 end) - sum(case when types.[value] = 'РеализацияТоваровИУслуг' then Количество.value else 0 end) 'Остаток'");
            //query.ДобавитьВычисляемыеКолонки("sum(case when nodes.[type] = 'ЭлементТовар' then Сумма.value else 0 end) - sum(case when nodes.[type] = 'РеализацияТоваровИУслуг' then Сумма.value else 0 end) 'Сумма'");
            query.ДобавитьВычисляемыеКолонки("sum(case when types.[value] = 'РеализацияТоваровИУслуг' then Сумма.value else 0 end) 'Сумма'");
            query.ДобавитьСортировку("СсылкаНаНоменклатуру", Query.НаправлениеСортировки.Asc);

            return data.Поиск(query, Хранилище.Оперативное, domain).AsEnumerable().Select(row =>
                new Инвентаризация()
                {
                    Товар = row.Field<decimal>("СсылкаНаНоменклатуру"),
                    КоличествоНаСкладе = Convert.ToDecimal(row["НаСкладе"]),
                    КоличествоПродано = Convert.ToDecimal(row["Продано"]),
                    КоличествоОстаток = Convert.ToDecimal(row["Остаток"]),
                    СуммаПродано = Convert.ToDecimal(row["Сумма"])
                });
        }
        #endregion
    }
}
