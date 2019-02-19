using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Documents;
using System.Windows.Xps.Packaging;
using System.Windows.Xps.Serialization;
using System.IO.Packaging;
using RosService.Intreface;
using System.Windows.Markup;
using System.Xml;
using System.Data;
using System.Threading;
using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Office.Interop.Word;
using Microsoft.Office.Interop.Excel;
using RosService.Data;
using RosService.Configuration;
using RosService.Finance;
using RosService.Files;
using System.Threading.Tasks;

namespace RosService.Helper
{
    internal class Отчет
    {
        private RosService.Data.DataClient data { get; set; }

        public class ПараметрComparer : IEqualityComparer<Query.Параметр>
        {
            public bool Equals(Query.Параметр x, Query.Параметр y)
            {
                return x.Имя == y.Имя;
            }

            public int GetHashCode(Query.Параметр obj)
            {
                return obj.GetHashCode();
            }
        }
        public Отчет()
        {
            this.data = new RosService.Data.DataClient();
        }

        protected string FileNameValid(string filename)
        {
            //return Regex.Replace(filename, @"[^\w +_()\.,\'""]", "");
            if (!string.IsNullOrEmpty(filename) && filename.IndexOfAny(Path.GetInvalidFileNameChars()) > 0)
            {
                foreach (var item in Path.GetInvalidFileNameChars())
                {
                    filename = filename.Replace(item, '_');
                }
            }
            return filename;
        }
        public decimal FindReport(string НазваниеОтчета, string domain)
        {
            var query = new Query();
            query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "НазваниеОбъекта", Значение = НазваниеОтчета });
            query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = 11942, МаксимальнаяГлубина = 0 });

            var report = data.Поиск(query, Хранилище.Конфигурация, domain).AsEnumerable().SingleOrDefault();
            if (report == null) throw new Exception(string.Format("Отчет '{0}' не найден", НазваниеОтчета));
            return report.Field<decimal>("id_node");
        }
        public IEnumerable<Query> BuildQuery(Query.Параметр[] Параметры, decimal id_report, string domain)
        {
            var Запросы = new List<Query>();
            var ПоисковыйЗапрос = data.ПолучитьЗначение<string>(id_report, "ПоисковыйЗапрос", Хранилище.Конфигурация, domain);
            if (!string.IsNullOrEmpty(ПоисковыйЗапрос))
            {
                if (ПоисковыйЗапрос.Contains("<Запрос>"))
                {
                    var xml = new XmlDocument();
                    xml.LoadXml(new Query().ParseSql(ПоисковыйЗапрос, domain));

                    foreach (XmlNode item in xml.SelectNodes("//Запрос"))
                    {
                        Запросы.Add(new Query() { СтрокаЗапрос = item.OuterXml, Параметры = new List<Query.Параметр>(Параметры ?? new Query.Параметр[0]) });
                    }
                }
                else
                {
                    foreach (Match item in Regex.Matches(ПоисковыйЗапрос, @"(\[[^\]]+\])", RegexOptions.Singleline | RegexOptions.Compiled))
                    {
                        Запросы.Add(new Query() { СтрокаЗапрос = item.Value, Параметры = new List<Query.Параметр>(Параметры ?? new Query.Параметр[0]) });
                    }
                }
            }
            return Запросы;
        }


        public DataSet BuildDataSet(string НазваниеОтчета, IEnumerable<Query> Запросы, RosService.Data.Хранилище Хранилище, string user, string domain)
        {
            var ds = new DataSet();
            //добавить параметры запроса
            var param = Запросы.Where(p => p != null).SelectMany(p => p.Параметры).Distinct(new ПараметрComparer());
            var table = new System.Data.DataTable("Параметры");
            table.Columns.Add("Имя", typeof(string));
            table.Columns.Add("Значение", typeof(object));

            if (param.FirstOrDefault(p => p.Имя.Equals("@Пользователь")) == null)
                table.Rows.Add(new object[] { "@Пользователь", user });
            if (param.FirstOrDefault(p => p.Имя.Equals("@Дата")) == null)
                table.Rows.Add(new object[] { "@Дата", DateTime.Now });
            if (param.FirstOrDefault(p => p.Имя.Equals("@НазваниеОтчета")) == null)
                table.Rows.Add(new object[] { "@НазваниеОтчета", НазваниеОтчета });
            if (Запросы != null)
            {
                foreach (var item in param)
                {
                    table.Rows.Add(new object[] { item.Имя, item.Значение });
                }
            }
            ds.Merge(table);

            //загрузить константы
            try
            {
                var query = new Query();
                //query.CacheDuration = TimeSpan.FromMinutes(5);
                //query.CacheLocation = Query.OutputCacheLocation.Memory;
                //query.CacheName = "ПечатьОтчетов/Константы"; 
                query.Типы.Add("Константа");
                query.ДобавитьВыводимыеКолонки(new string[] { "НазваниеОбъекта", "ЗначениеКонстанты" });
                query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = "Константы", МаксимальнаяГлубина = 1 });
                table = data.Поиск(query, Хранилище.Оперативное, domain).Значение;
                table.TableName = "Константа";
                ds.Merge(table);
            }
            catch (Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку("СформироватьОтчет.BuildDataSet: Константа", ex.ToString(), user, domain);
            }

            if (Запросы != null)
            {
                var lockObject = new System.Object();
                var oRow = 0;
                foreach (var _query in Запросы.Where(p => p != null))
                {
                    var table1 = null as System.Data.DataTable;
                    if (_query.СтрокаЗапрос != null && _query.СтрокаЗапрос.StartsWith("[ПоискИстории;"))
                    {
                        table1 = data.ПоискИстории(_query, Хранилище, domain).Значение;
                    }
                    else
                    {
                        table1 = data.Поиск(_query, Хранилище, domain).Значение;
                    }
                    table1.TableName = string.Format("Значение{0}", oRow + 1);
                    var colums = new List<string>();
                    foreach (DataColumn _item in table1.Columns)
                    {
                        if (!(_item.DataType == typeof(double) || _item.DataType == typeof(decimal))) continue;
                        if (_item.ColumnName == "id_node" || _item.ColumnName == "type") continue;
                        colums.Add(_item.ColumnName);
                    }
                    foreach (var _item in colums)
                    {
                        var column = table1.Columns.Add(_item + "Прописью", typeof(string));
                        var column2 = table1.Columns.Add(_item + "ПрописьюПолностью", typeof(string));
                        foreach (var row in table1.AsEnumerable())
                        {
                            var p = Convert.IsDBNull(row[_item]) ? 0 : Convert.ToDouble(row[_item]);
                            row[column] = new FinanceClient().ЧислоПрописью(p, ФорматЧислаПрописью.РублиБезКопеек).Trim();
                            row[column2] = new FinanceClient().ЧислоПрописью(p, ФорматЧислаПрописью.Рубли).Trim();
                        }
                    }
                    lock (lockObject)
                    {
                        ds.Merge(table1);
                    }
                    oRow++;
                }//);
            }
            return ds;
        }
        public Файл ВыводОтчета(string НазваниеОтчета, string ИсходныйКод, string ИсходныйФорматОтчета, ФорматОтчета ФорматОтчета, ШаблонОтчета.Ориентация Ориентация, DataSet ds, Хранилище Хранилище, string user, string domain)
        {
            var file = null as Файл;
            try
            {
                if (ds == null) new Exception("Не определен DataSet");

                var extension = string.Empty;
                var mime = MimeType.НеОпределен;
                switch (ИсходныйФорматОтчета)
                {
                    case "Xps":
                        extension = "xps";
                        mime = MimeType.Xps;
                        break;
                    case "Excel":
                        extension = "xls";
                        mime = MimeType.Excel;
                        break;
                    case "Word":
                        extension = "doc";
                        mime = MimeType.Word;
                        break;
                    case "Text Plain":
                        extension = "txt";
                        mime = MimeType.Text;
                        break;
                    case "Excel-PDF":
                    case "Word-PDF":
                        extension = "pdf";
                        break;
                    default:
                        throw new Exception("Не верно указан 'Исходный формат отчета'");
                }
                file = new Файл() { Name = string.Format("{0}_{1:yyyymmdd_HHmmss}.{2}", FileNameValid(НазваниеОтчета), DateTime.Now, extension), MimeType = mime };
                var task = new System.Threading.Tasks.Task(() =>
                {
                    try
                    {
                        using (var msXml = new MemoryStream())
                        {
                            var processor = new Saxon.Api.Processor();
                            var compiler = processor.NewXsltCompiler();
                            var transformer = compiler.Compile(new StringReader(ИсходныйКод)).Load();
                            var dest = new Saxon.Api.TextWriterDestination(XmlTextWriter.Create(msXml));

                            transformer.InitialContextNode = processor.NewDocumentBuilder().Build(new XmlDataDocument(ds));
                            transformer.Run(dest);

                            switch (ИсходныйФорматОтчета)
                            {
                                case "Xps":
                                    {
                                        file.MimeType = MimeType.Xps;
                                        switch (ФорматОтчета)
                                        {
                                            #region Отчет Xaml (по-умолчанию)
                                            case ФорматОтчета.Xaml:
                                            case ФорматОтчета.ПоУмолчанию:
                                                {
                                                    file.Stream = msXml.ToArray();
                                                }
                                                break;
                                            #endregion

                                            #region Отчет Xps
                                            case ФорматОтчета.Xps:
                                            default:
                                                {
                                                    var documentUri = Path.GetTempFileName();
                                                    try
                                                    {
                                                        msXml.Position = 0;
                                                        var sourceDoc = XamlReader.Load(msXml) as IDocumentPaginatorSource;

                                                        var doc = new XpsDocument(documentUri, FileAccess.Write, CompressionOption.Normal);
                                                        var xsm = new XpsSerializationManager(new XpsPackagingPolicy(doc), false);
                                                        var pgn = sourceDoc.DocumentPaginator;
                                                        // 1cm = 38px
                                                        switch (Ориентация)
                                                        {
                                                            case ШаблонОтчета.Ориентация.Альбом:
                                                                {
                                                                    pgn.PageSize = new System.Windows.Size(29.7 * (96 / 2.54), 21 * (96 / 2.54));
                                                                }
                                                                break;

                                                            case ШаблонОтчета.Ориентация.Книга:
                                                            default:
                                                                {
                                                                    pgn.PageSize = new System.Windows.Size(21 * (96 / 2.54), 29.7 * (96 / 2.54));
                                                                }
                                                                break;
                                                        }
                                                        //необходимо фиксированно указать размер колонки документа иначе при 
                                                        //построении часть данных будет срезано
                                                        if (sourceDoc is FlowDocument)
                                                        {
                                                            ((FlowDocument)sourceDoc).ColumnWidth = pgn.PageSize.Width;
                                                        }
                                                        xsm.SaveAsXaml(pgn);

                                                        doc.Close();
                                                        file.Stream = System.IO.File.ReadAllBytes(documentUri);
                                                    }
                                                    finally
                                                    {
                                                        if (System.IO.File.Exists(documentUri))
                                                            System.IO.File.Delete(documentUri);
                                                    }
                                                }
                                                break;
                                            #endregion
                                        }
                                    }
                                    break;

                                case "TextPlain":
                                    {
                                        file.MimeType = MimeType.Text;
                                        file.Stream = msXml.ToArray();
                                    }
                                    break;

                                case "Word-PDF":
                                    {
                                        #region generate
                                        object paramSourceDocPath = Path.GetTempFileName();
                                        object paramMissing = System.Type.Missing;

                                        System.IO.File.WriteAllBytes((string)paramSourceDocPath, msXml.ToArray());
                                        if (!System.IO.File.Exists((string)paramSourceDocPath))
                                            throw new Exception(string.Format("Исходный файл для генерации отчета не найден '{0}'", paramSourceDocPath));

                                        var wordApplication = null as Microsoft.Office.Interop.Word.Application;
                                        var wordDocument = null as Document;
                                        var paramExportFilePath = GetTempFilePathWithExtension(".pdf");
                                        try
                                        {
                                            wordApplication = new Microsoft.Office.Interop.Word.Application() { DisplayAlerts = WdAlertLevel.wdAlertsNone };

                                            var paramExportFormat = WdExportFormat.wdExportFormatPDF;
                                            var paramOpenAfterExport = false;
                                            var paramExportOptimizeFor = WdExportOptimizeFor.wdExportOptimizeForPrint;
                                            var paramExportRange = WdExportRange.wdExportAllDocument;
                                            var paramStartPage = 0;
                                            var paramEndPage = 0;
                                            var paramExportItem = WdExportItem.wdExportDocumentContent;
                                            var paramIncludeDocProps = true;
                                            var paramKeepIRM = true;
                                            var paramCreateBookmarks = WdExportCreateBookmarks.wdExportCreateWordBookmarks;
                                            var paramDocStructureTags = true;
                                            var paramBitmapMissingFonts = true;
                                            var paramUseISO19005_1 = false;

                                            // Open the source document.
                                            wordDocument = wordApplication.Documents.Open(
                                                ref paramSourceDocPath, ref paramMissing, ref paramMissing,
                                                ref paramMissing, ref paramMissing, ref paramMissing,
                                                ref paramMissing, ref paramMissing, ref paramMissing,
                                                ref paramMissing, ref paramMissing, ref paramMissing,
                                                ref paramMissing, ref paramMissing, ref paramMissing,
                                                ref paramMissing);

                                            // Export it in the specified format.
                                            if (wordDocument != null)
                                                wordDocument.ExportAsFixedFormat(paramExportFilePath,
                                                    paramExportFormat, paramOpenAfterExport,
                                                    paramExportOptimizeFor, paramExportRange, paramStartPage,
                                                    paramEndPage, paramExportItem, paramIncludeDocProps,
                                                    paramKeepIRM, paramCreateBookmarks, paramDocStructureTags,
                                                    paramBitmapMissingFonts, paramUseISO19005_1,
                                                    ref paramMissing);

                                            if (!System.IO.File.Exists(paramExportFilePath))
                                                throw new Exception(string.Format("Word-PDF: Файл отчета не найден '{0}'", paramExportFilePath));

                                            if (Enum.IsDefined(typeof(MimeType), ИсходныйФорматОтчета))
                                                file.MimeType = (MimeType)Enum.Parse(typeof(MimeType), ИсходныйФорматОтчета);

                                            file.Stream = System.IO.File.ReadAllBytes(paramExportFilePath);

                                        }
                                        finally
                                        {
                                            // Close and release the Document object.
                                            if (wordDocument != null)
                                            {
                                                wordDocument.Close(false, ref paramMissing, ref paramMissing);
                                                wordDocument = null;
                                            }

                                            // Quit Word and release the ApplicationClass object.
                                            if (wordApplication != null)
                                            {
                                                wordApplication.Quit(false, ref paramMissing, ref paramMissing);
                                                wordApplication = null;

                                                GC.Collect();
                                                GC.WaitForPendingFinalizers();
                                                GC.Collect();
                                            }

                                            if (System.IO.File.Exists((string)paramSourceDocPath))
                                                System.IO.File.Delete((string)paramSourceDocPath);
                                            if (System.IO.File.Exists(paramExportFilePath))
                                                System.IO.File.Delete(paramExportFilePath);

                                            //var sb = new StringBuilder();
                                            //sb.AppendLine((string)paramSourceDocPath);
                                            //sb.AppendLine(paramExportFilePath);
                                            //ConfigurationClient.WindowsLog(sb.ToString(), "", domain);
                                        }
                                        #endregion
                                    }
                                    break;

                                case "Excel-PDF":
                                    {
                                        #region generate
                                        object paramSourceDocPath = Path.GetTempFileName();
                                        object paramMissing = System.Type.Missing;

                                        System.IO.File.WriteAllBytes((string)paramSourceDocPath, msXml.ToArray());
                                        if (!System.IO.File.Exists((string)paramSourceDocPath))
                                            throw new Exception(string.Format("Исходный файл для генерации отчета не найден '{0}'", paramSourceDocPath));

                                        var excelApplication = new Microsoft.Office.Interop.Excel.Application() { DisplayAlerts = false };
                                        var excelDocument = null as Workbook;

                                        var paramExportFilePath = GetTempFilePathWithExtension(".pdf");
                                        var paramExportFormat = XlFixedFormatType.xlTypePDF;
                                        var paramExportQuality = XlFixedFormatQuality.xlQualityStandard;
                                        var paramOpenAfterPublish = false;
                                        var paramIncludeDocProps = true;
                                        var paramIgnorePrintAreas = true;
                                        var paramFromPage = System.Type.Missing;
                                        var paramToPage = System.Type.Missing;

                                        try
                                        {
                                            // Open the source document.
                                            excelDocument = excelApplication.Workbooks.Open(
                                                (string)paramSourceDocPath, paramMissing, paramMissing,
                                                paramMissing, paramMissing, paramMissing,
                                                paramMissing, paramMissing, paramMissing,
                                                paramMissing, paramMissing, paramMissing,
                                                paramMissing, paramMissing, paramMissing);

                                            // Export it in the specified format.
                                            if (excelDocument != null)
                                                excelDocument.ExportAsFixedFormat(paramExportFormat,
                                                    paramExportFilePath, paramExportQuality,
                                                    paramIncludeDocProps, paramIgnorePrintAreas, paramFromPage,
                                                    paramToPage, paramOpenAfterPublish,
                                                    paramMissing);

                                            if (!System.IO.File.Exists(paramExportFilePath))
                                                throw new Exception(string.Format("Файл отчета не найден '{0}'", paramExportFilePath));

                                            if (Enum.IsDefined(typeof(MimeType), ИсходныйФорматОтчета))
                                                file.MimeType = (MimeType)Enum.Parse(typeof(MimeType), ИсходныйФорматОтчета);

                                            file.Stream = System.IO.File.ReadAllBytes(paramExportFilePath);

                                        }
                                        finally
                                        {
                                            // Close and release the Document object.
                                            if (excelDocument != null)
                                            {
                                                excelDocument.Close(false, paramMissing, paramMissing);
                                                excelDocument = null;
                                            }

                                            // Quit Word and release the ApplicationClass object.
                                            if (excelApplication != null)
                                            {
                                                excelApplication.Quit();
                                                excelApplication = null;
                                            }

                                            if (System.IO.File.Exists((string)paramSourceDocPath))
                                                System.IO.File.Delete((string)paramSourceDocPath);
                                            if (System.IO.File.Exists(paramExportFilePath))
                                                System.IO.File.Delete(paramExportFilePath);

                                            GC.Collect();
                                            GC.WaitForPendingFinalizers();
                                            GC.Collect();
                                            GC.WaitForPendingFinalizers();
                                        }
                                        #endregion
                                    }
                                    break;

                                default:
                                    {
                                        if (Enum.IsDefined(typeof(MimeType), ИсходныйФорматОтчета))
                                            file.MimeType = (MimeType)Enum.Parse(typeof(MimeType), ИсходныйФорматОтчета);

                                        file.Stream = msXml.ToArray();
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "ВыводОтчета");
                    }
                });
                task.Start();
                task.Wait();
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "ВыводОтчета");
            }
            return file;
        }
        public Файл СформироватьОтчет(string НазваниеОтчета, Query.Параметр[] Параметры, ФорматОтчета ФорматОтчета, Хранилище Хранилище, string user, string domain)
        {
            var id_report = FindReport(НазваниеОтчета, domain);
            var ИсходныйКод = data.ПолучитьЗначение<string>(id_report, "ИсходныйКод", Хранилище.Конфигурация, domain);
            var Запросы = BuildQuery(Параметры, id_report, domain);

            if (string.IsNullOrEmpty(ИсходныйКод))
            {
                if (Запросы != null && Запросы.Count() > 0)
                {
                    var Шаблон = new ШаблонОтчета()
                    {
                        ВерхнийКолонтитул = true,
                        НижнийКолонтитул = true,
                        ОриентацияСтраницы = ШаблонОтчета.Ориентация.Альбом,
                        НазваниеОтчета = НазваниеОтчета,
                        Запросы = Запросы.Select(p => Serializer(p)).ToArray()
                    };
                    var колонки = new List<ШаблонОтчета.Колонка>();
                    var firsQuery = Запросы.ElementAt(0);
                    firsQuery.Parse(domain);
                    foreach (var item in firsQuery.ВыводимыеКолонки)
                    {
                        var type = new ConfigurationClient().ПолучитьТип(item.Атрибут, domain);
                        if (type == null) continue;

                        var __Колонка = new ШаблонОтчета.Колонка() { Атрибут = item.Атрибут, Название = type.Описание };
                        switch (type.MemberType)
                        {
                            case MemberTypes.String:
                                __Колонка.Размер = 160;
                                break;
                            case MemberTypes.Double:
                            case MemberTypes.Int:
                                __Колонка.Размер = 100;
                                break;
                            case MemberTypes.DateTime:
                                __Колонка.Размер = 80;
                                break;
                            case MemberTypes.Bool:
                                __Колонка.Размер = 40;
                                break;
                            case MemberTypes.Ссылка:
                                __Колонка.Размер = 200;
                                __Колонка.Атрибут = item.Атрибут + ".НазваниеОбъекта";
                                break;
                        }
                        колонки.Add(__Колонка);
                    }
                    Шаблон.Колонки = колонки.ToArray();
                    return СформироватьОтчет(Шаблон, Хранилище, user, domain);
                }
                else
                {
                    return new Файл();
                }
            }
            else
            {
                #region Подготовить DataSet
                var ds = BuildDataSet(НазваниеОтчета, Запросы, Хранилище, user, domain);
                #endregion

                return ВыводОтчета(
                    НазваниеОтчета,
                    ИсходныйКод,
                    data.ПолучитьЗначение<string>(id_report, "ФорматОтчета", Хранилище.Конфигурация, domain),
                    ФорматОтчета,
                    (ШаблонОтчета.Ориентация)Enum.Parse(typeof(ШаблонОтчета.Ориентация), data.ПолучитьЗначение<string>(id_report, "Ориентация", Хранилище.Конфигурация, domain)),
                    ds, Хранилище, user, domain);
            }
        }
        public Файл СформироватьОтчет(ШаблонОтчета Шаблон, Хранилище Хранилище, string user, string domain)
        {
            if (Шаблон.Колонки == null)
                Шаблон.Колонки = new ШаблонОтчета.Колонка[0];
            else
                Шаблон.Колонки = Шаблон.Колонки.Where(p => !string.IsNullOrEmpty(p.Атрибут)).ToArray();

            switch (Шаблон.ФорматОтчета)
            {
                //case ФорматОтчета.ПоУмолчанию:
                case ФорматОтчета.Xaml:
                case ФорматОтчета.Xps:
                    throw new Exception(string.Format("Формат '{0}' не поддерживается конструктором отчетов", Шаблон.ФорматОтчета.ToString()));
            }
            var sb = new StringBuilder(10000);

            #region Подготовить DataSet
            var querys = Шаблон.Запросы.Select(p => Query.Deserialize(p)).ToArray();
            foreach (var item in querys.Where(p => p != null))
                item.ИгнорироватьСтраницы = true;
            var ds = BuildDataSet(Шаблон.НазваниеОтчета, querys, Хранилище, user, domain);
            #endregion

            #region generate columns
            var table = new System.Data.DataTable("Columns");
            table.Columns.Add("Атрибут", typeof(string));
            table.Columns.Add("Название", typeof(string));
            table.Columns.Add("Размер", typeof(int));
            foreach (var item in Шаблон.Колонки)
            {
                table.Rows.Add(new object[] { item.Атрибут, item.Название, !double.IsNaN(item.Размер) ? item.Размер : 140 });
            }
            ds.Merge(table);
            #endregion

            #region Columns Display
            sb = new StringBuilder();

            var source = "//Значение1";
            if (Шаблон.Группировки != null)
            {
                foreach (var item in Шаблон.Группировки)
                {
                    var _Группировки = item.Trim().Replace("/", "_x002F_").Replace("@", "_x0040_");
                    sb.AppendFormat(@"<xsl:for-each-group select=""{0}"" group-by=""{1}"">", source, _Группировки);
                    sb.AppendLine();
                    sb.AppendLine(@"<xsl:sort select=""current-grouping-key()""/>");
                    sb.AppendLine(@"<Row>");
                    sb.AppendFormat("\t<Cell ss:MergeAcross=\"{0}\" ss:StyleID=\"s177\">", Шаблон.Колонки.Length - 1);
                    sb.AppendLine();
                    sb.AppendLine("\t\t<Data ss:Type=\"String\"><xsl:value-of select=\"current-grouping-key()\"/></Data>");
                    sb.AppendLine("\t</Cell>");
                    sb.AppendLine(@"</Row>");
                    source = "current-group()";
                }
            }

            #region <Header>
            sb.Append(@"
<Row ss:Height=""13.5"">
<xsl:for-each select=""//Columns"">
    <Cell ss:StyleID=""s97"">
    <Data ss:Type=""String"">
        <xsl:value-of select=""Название"" />
    </Data>
    </Cell>
</xsl:for-each>
</Row>
");
            #endregion

            #region <Row>
            var isDataSet = ds != null && ds.Tables.Contains("Значение1");
            sb.AppendFormat(@"<xsl:for-each select=""{0}"">", source);
            sb.AppendLine(@"<Row>");
            foreach (var item in Шаблон.Колонки)
            {
                var style = "s93";
                var type = null as Configuration.Type;
                if (!string.IsNullOrEmpty(item.Формат))
                {
                    switch (item.Формат)
                    {
                        case "Int":
                            type = new Configuration.Type() { MemberType = MemberTypes.Int };
                            break;

                        case "Double":
                            type = new Configuration.Type() { MemberType = MemberTypes.Double };
                            break;

                        case "String":
                            type = new Configuration.Type() { MemberType = MemberTypes.String };
                            break;
                    }
                }

                if (type == null && isDataSet && ds.Tables["Значение1"].Columns.Contains(item.Атрибут))
                {
                    //определить тип из DataSet
                    var typeofColumn = ds.Tables["Значение1"].Columns[item.Атрибут].DataType;
                    if (typeofColumn == typeof(double) || typeofColumn == typeof(decimal))
                        type = new Configuration.Type() { MemberType = MemberTypes.Double };

                    else if (typeofColumn == typeof(int) || typeofColumn == typeof(long))
                        type = new Configuration.Type() { MemberType = MemberTypes.Int };

                    else if (typeofColumn == typeof(DateTime))
                        type = new Configuration.Type() { MemberType = MemberTypes.DateTime };

                    else if (typeofColumn == typeof(bool))
                        type = new Configuration.Type() { MemberType = MemberTypes.Bool };

                    else
                        type = new Configuration.Type() { MemberType = MemberTypes.String };
                }
                else
                {
                    type = new ConfigurationClient().ПолучитьТип(item.Атрибут, domain);
                }

                #region set style
                if (type != null)
                {
                    switch (type.MemberType)
                    {
                        case MemberTypes.Double:
                            style = "s93Numeric";
                            break;

                        case MemberTypes.Ссылка:
                        case MemberTypes.Int:
                            style = "s93Int";
                            break;
                    }
                }
                #endregion

                sb.AppendFormat("<Cell ss:StyleID=\"{0}\">", style);
                sb.AppendLine();
                if (!string.IsNullOrEmpty(item.Атрибут))
                {
                    var attr = item.Атрибут.Replace("/", "_x002F_").Replace("@", "_x0040_");
                    if (type != null)
                    {
                        switch (type.MemberType)
                        {
                            case MemberTypes.Double:
                            case MemberTypes.Ссылка:
                            case MemberTypes.Int:
                                sb.Append(@"<Data ss:Type=""Number"">");
                                sb.AppendFormat(@"<xsl:value-of select=""{0}"" />", attr);
                                //sb.AppendFormat(@"<xsl:value-of select=""format-number({0},'# ##0,00')"" />", item.Атрибут);
                                break;

                            case MemberTypes.DateTime:
                                sb.Append(@"<Data ss:Type=""String"">");
                                switch ((item.Формат ?? "").ToUpper())
                                {
                                    case "G":
                                        sb.AppendFormat(@"<xsl:value-of select=""format-dateTime({0},'[D,2].[M,2].[Y,*-4] [H01]:[m01]')"" />", attr);
                                        break;

                                    case "HH:MM":
                                        sb.AppendFormat(@"<xsl:value-of select=""format-dateTime({0},'[H01]:[m01]')"" />", attr);
                                        break;

                                    default:
                                        sb.AppendFormat(@"<xsl:value-of select=""format-dateTime({0},'[D,2].[M,2].[Y,*-4]')"" />", attr);
                                        break;
                                }
                                break;

                            //case MemberTypes.Bool:
                            //    sb.Append(@"<Data ss:Type=""String"">");
                            //    sb.AppendFormat(@"<xsl:if test=""{0} = 'true'"">ДА</<xsl:if>", attr);
                            //    break;

                            default:
                                sb.Append(@"<Data ss:Type=""String"">");
                                sb.AppendFormat(@"<xsl:value-of select=""{0}"" />", attr);
                                break;
                        }
                    }
                    else
                    {
                        sb.AppendFormat(@"<Data ss:Type=""{0}"">", item.ТипЗначения.ToString());
                        sb.AppendFormat(@"<xsl:value-of select=""{0}"" />", attr);
                    }
                    sb.AppendLine(@"</Data>");
                }
                sb.AppendLine(@"</Cell>");
            }
            sb.AppendLine(@"</Row>");
            sb.AppendLine(@"</xsl:for-each>");
            sb.AppendLine(@"<Row />");
            sb.AppendLine(@"<Row />");
            #endregion

            if (Шаблон.Группировки != null)
            {
                foreach (var item in Шаблон.Группировки)
                {
                    sb.AppendLine(@"</xsl:for-each-group>");
                }
            }
            #endregion

            #region ИсходныйКод
            var ИсходныйКод = Regex.Replace(RosService.Properties.Resources.ReportExcel, "<r:Data />", sb.ToString());
            #endregion

            return ВыводОтчета(
                Шаблон.НазваниеОтчета,
                ИсходныйКод.ToString(),
                "Excel",
                Шаблон.ФорматОтчета,
                Шаблон.ОриентацияСтраницы,
                ds,
                Хранилище,
                user,
                domain);
        }


        private static string GetTempFilePathWithExtension(string extension)
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "-" + DateTime.Now.Ticks.ToString() + extension);
        }
        private static string Serializer(Query query)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Query));
            var sb = new System.Text.StringBuilder();
            using (var sw = new System.IO.StringWriter(sb))
            {
                serializer.Serialize(sw, query);
                return sb.ToString();
            }
        }
    }
}
