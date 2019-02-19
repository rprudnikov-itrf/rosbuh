using System;
using System.Linq;
using RosService.Intreface;
using RosService.DataClasses;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Windows.Controls;
using System.ServiceModel;
using System.Data;
using System.Transactions;
using RosService.Data;
using RosService.Configuration;
using RosService.Caching;
using System.Threading.Tasks;
using FloodFill2;


namespace RosService.Files
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall,
    AddressFilterMode = AddressFilterMode.Any,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false,
    ConfigurationName = "RosService.File")]
    public partial class FileClient : IFile
    {
        #region Файлы получить
        public DownloadResponce ПолучитьФайл(DownloadRequest request)
        {
            var data = new DataClient();
            var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"], request.domain);
            var values = data.ПолучитьЗначение(request.id_file, new string[] { "НазваниеОбъекта", "ПолноеИмяФайла" }, request.хранилище, request.domain);
            var filename = Path.Combine(path, Convert.ToString(values["ПолноеИмяФайла"].Значение));
            var responce = new DownloadResponce() { FileName = Convert.ToString(values["НазваниеОбъекта"].Значение) };
            if (System.IO.File.Exists(filename))
            {
                responce.Length = new FileInfo(filename).Length;
                responce.FileByteStream = System.IO.File.OpenRead(filename);
            }
            else
            {
                throw new Exception(string.Format("Файл '{0}' не найден.", responce.FileName));
            }
            return responce;
        }
        public byte[] ПолучитьФайлПолностью(object id_file, Хранилище хранилище, string domain)
        {
            var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"], domain);
            var ПолноеИмяФайла = new DataClient().ПолучитьЗначение<string>(id_file, "ПолноеИмяФайла", хранилище, domain);
            if (string.IsNullOrEmpty(ПолноеИмяФайла))
                return null;

            var filename = Path.Combine(path, ПолноеИмяФайла);
            if (System.IO.File.Exists(filename))
            {
                return System.IO.File.ReadAllBytes(filename);
            }
            else
            {
                return null;
            }
        }
        public byte[] ПолучитьФайлПолностьюПоНазванию(object id_node, string ИдентификаторФайла, Хранилище хранилище, string domain)
        {
            var file = new DataClient()._СписокФайлов(id_node, хранилище, domain).FirstOrDefault(p => p.ИдентификаторФайла == ИдентификаторФайла);
            if (file == null) 
                return null;

            return ПолучитьФайлПолностью(file.id_node, хранилище, domain);

            //var query = new Query();
            //query.КоличествоВыводимыхДанных = 1;
            //query.КоличествоВыводимыхСтраниц = 1;
            //query.Типы.Add("Файл%");
            //query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИдентификаторОбъекта", Значение = ИдентификаторФайла });
            //query.ДобавитьВыводимыеКолонки(new string[] { "ИдентификаторОбъекта" });
            //query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = id_node, МаксимальнаяГлубина = 1 });
            //var file = new DataClient().Поиск(query, хранилище, domain).AsEnumerable().SingleOrDefault();
            //if (file == null) return null;
        }
        #endregion

        #region Файлы сохранить
        public UploadResponce СохранитьФайл(UploadRequest request)
        {
            var data = new DataClient();
            try
            {
                var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"], request.domain);
                if (!Directory.Exists(path)) 
                    Directory.CreateDirectory(path);

                var tmp_file = Path.Combine(path, Path.GetRandomFileName());
                using (var file = System.IO.File.Create(tmp_file))
                {
                    var chunkSize = 1024 * 64;
                    var buffer = new byte[chunkSize];
                    var bytesRead = 0;
                    while ((bytesRead = request.FileByteStream.Read(buffer, 0, chunkSize)) > 0)
                    {
                        file.Write(buffer, 0, bytesRead);
                    }
                }

                //удалить старый файл
                if (!string.IsNullOrEmpty(request.ИдентификаторФайла))
                {
                    var file = new DataClient()._СписокФайлов(request.id_node, request.хранилище, request.domain).FirstOrDefault(p => p.ИдентификаторФайла == request.ИдентификаторФайла);
                    if (file != null)
                    {
                        data.УдалитьРаздел(false, false, new decimal[] { file.id_node }, request.хранилище, request.user, request.domain);
                    }
                }

                //Проверить существует ли хранилище
                if (string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"]))
                    throw new Exception("Не задан путь к файловому хранилищу в файле конфигурации 'appSettings//ОперативноеХранилище'");


                var filename = string.Format("{0}", request.ИмяФайла.Replace(" ", "_"));

                //если добавляемый файл существует, заменить имя
                var full_filename = Path.Combine(path, filename);
                if (System.IO.File.Exists(full_filename))
                {
                    var Extension = Path.GetExtension(filename);
                    var FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                    for (int i = 1; ; i++)
                    {
                        filename = string.Format("{0}_{1:f0}{2}", FileNameWithoutExtension, i, Extension);
                        full_filename = Path.Combine(path, filename);

                        if (!System.IO.File.Exists(full_filename))
                            break;
                    }
                }
                if (System.IO.File.Exists(tmp_file))
                {
                    System.IO.File.Move(tmp_file, full_filename);
                }

                //сохранить новый файл
                var values = new Dictionary<string, Value>();
                values.Add("НазваниеОбъекта", new Value(request.ИмяФайла));
                values.Add("ИдентификаторОбъекта", new Value(request.ИдентификаторФайла));
                values.Add("ОписаниеФайла", new Value(request.Описание));
                values.Add("РазмерФайла", new Value(request.Length));
                values.Add("MimeType", new Value(GetMimeType(request.ИмяФайла)));
                values.Add("ПолноеИмяФайла", new Value(filename));
                var id_file = data.ДобавитьРаздел(request.id_node, "Файл", values, false, request.хранилище, true, request.user, request.domain);

                #region Обновить кол-во в кеше
                var __keyfull = MemoryCache.Path(request.domain, request.хранилище, request.id_node) + "@@КоличествоФайлов";
                var _tmp = MemoryCache.Get(__keyfull);
                if (_tmp != null)
                {
                    _tmp.obj.Значение = Convert.ToInt32(_tmp.obj.Значение) + 1;
                    if (MemoryCache.IsMemoryCacheClient)
                        MemoryCache.Set(__keyfull, _tmp);
                }
                #endregion

                return new UploadResponce() { id_file = id_file };
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), request.user, request.domain, "СохранитьФайл", request.ИмяФайла);
                return new UploadResponce();
            }
            finally
            {
                request.Dispose();
            }
        }
        public decimal СохранитьФайлПолностью(object id_node, string ИдентификаторФайла, string ИмяФайла, string Описание, byte[] stream, Хранилище хранилище, string user, string domain)
        {
            try
            {
                var data = new DataClient();

                //удалить старый файл
                if (!string.IsNullOrEmpty(ИдентификаторФайла))
                {
                    var file = new DataClient()._СписокФайлов(id_node, хранилище, domain).FirstOrDefault(p => p.ИдентификаторФайла == ИдентификаторФайла);
                    if (file != null)
                    {
                        data.УдалитьРаздел(false, false, new decimal[] { file.id_node }, хранилище, user, domain);
                    }
                }

                //Проверить существует ли хранилище
                if (string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"]))
                    throw new Exception("Не задан путь к файловому хранилищу в файле конфигурации 'appSettings//ОперативноеХранилище'");

                var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ОперативноеХранилище"], domain);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                #region save file
                var filename = string.Format("{0}", ИмяФайла.Replace(" ", "_"));
                if (stream != null)
                {
                    var full_filename = Path.Combine(path, filename);
                    if (System.IO.File.Exists(full_filename))
                    {
                        var Extension = Path.GetExtension(filename);
                        var FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                        for (int i = 1; ; i++)
                        {
                            filename = string.Format("{0}_{1:f0}{2}", FileNameWithoutExtension, i, Extension);
                            full_filename = Path.Combine(path, filename);

                            if (!System.IO.File.Exists(full_filename))
                                break;
                        }
                    }

                    System.IO.File.WriteAllBytes(full_filename, stream);

                    //#region асинхронное сохранение на диск
                    //System.Threading.Tasks.Task.Factory.StartNew((object obj) =>
                    //{
                    //    try
                    //    {
                    //        System.IO.File.WriteAllBytes((string)((object[])obj)[0], (byte[])((object[])obj)[1]);
                    //    }
                    //    catch (Exception)
                    //    {
                    //    }
                    //}, new object[] { full_filename, stream });
                    //#endregion
                }
                #endregion

                //сохранить новый файл
                var values = new Dictionary<string, Value>(6);
                values.Add("НазваниеОбъекта", new Value(ИмяФайла));
                values.Add("ИдентификаторОбъекта", new Value(ИдентификаторФайла));
                values.Add("ОписаниеФайла", new Value(Описание));
                values.Add("ПолноеИмяФайла", new Value(filename));
                values.Add("РазмерФайла", new Value(stream == null ? 0 : stream.Length));
                values.Add("MimeType", new Value(GetMimeType(ИмяФайла)));
                var id_file = data.ДобавитьРаздел(id_node, "Файл", values, false, хранилище, false, user, domain);

                #region Обновить кол-во в кеше
                var __keyfull = MemoryCache.Path(domain, хранилище, Convert.ToDecimal(id_node)) + "@@КоличествоФайлов";
                var _tmp = MemoryCache.Get(__keyfull);
                if (_tmp != null)
                {
                    _tmp.obj.Значение = Convert.ToInt32(_tmp.obj.Значение) + 1;
                    if (MemoryCache.IsMemoryCacheClient)
                        MemoryCache.Set(__keyfull, _tmp);
                }
                #endregion

                return id_file;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), user, domain, "СохранитьФайлПолностью");
                return 0;
            }
        }
        public void СохранитьФайлПолностьюАсинхронно(object id_node, string ИдентификаторФайла, string ИмяФайла, string Описание, byte[] stream, Хранилище хранилище, string user, string domain)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                СохранитьФайлПолностью(id_node, ИдентификаторФайла, ИмяФайла, Описание, stream, хранилище, user, domain);
            });
        }
        #endregion

        #region Просмотр
        //protected static readonly object trimmingLockObject = new System.Object();
        public byte[] ПолучитьФайлПросмотр(object id_file, ImageType type, int width, int height, ImageFormat format, long Качество, КонструкторИзображения Конструктор, string domain)
        {
            #region FileName
            var data = new DataClient();
            var path = Path.Combine(System.Configuration.ConfigurationManager.AppSettings["ПросмотрХранилище"], domain);
            var filename = null as string;

            switch (format)
            {
                case ImageFormat.Jpg:
                    {
                        filename = string.Format("{0:f0}_{1:0}x{2:0}_{3}_{4}_{5}.jpg",
                            id_file, width, height,
                            type.ToString(), Качество,
                            Path.GetFileNameWithoutExtension(data.ПолучитьЗначение<string>(id_file, "НазваниеОбъекта", Хранилище.Оперативное, domain)));
                    }
                    break;

                case ImageFormat.Png:
                    {
                        filename = string.Format("{0:f0}_{1:0}x{2:0}_{3}_{4}.png",
                            id_file, width, height,
                            type.ToString(),
                            Path.GetFileNameWithoutExtension(data.ПолучитьЗначение<string>(id_file, "НазваниеОбъекта", Хранилище.Оперативное, domain)));
                    }
                    break;
            }
            #endregion

            var full_filename = Path.Combine(path, filename);
            if (!Directory.Exists(path)) 
                Directory.CreateDirectory(path);

            if (System.IO.File.Exists(full_filename))
            {
                return System.IO.File.ReadAllBytes(full_filename);
            }
            else
            {
                var imgOutput = null as Bitmap;
                var canvas = null as Graphics;
                try
                {
                    using (var output = new MemoryStream())
                    {
                        try
                        {
                            var image = null as Bitmap;
                            var filestream = ПолучитьФайлПолностью(id_file, Хранилище.Оперативное, domain);
                            if (filestream != null)
                            {
                                using (var mc = new MemoryStream(filestream))
                                using (image = new Bitmap(mc))
                                {
                                    #region очистить фон
                                    if (Конструктор != null && Конструктор.ПрозрачныйФон)
                                    {
                                        var firstPixelColor = image.GetPixel(0, 0);
                                        if (firstPixelColor != null && Color.Transparent.ToArgb() != firstPixelColor.ToArgb())
                                        {

                                            var floodFiller = new UnsafeQueueLinearFloodFiller()
                                            {
                                                Bitmap = new PictureBoxScroll.EditableBitmap(image, PixelFormat.Format32bppArgb),
                                                FillColor = Color.Transparent,
                                                Tolerance = new byte[] { 5, 5, 5 }
                                            };
                                            floodFiller.FloodFill(new Point(0, 0));

                                            if (floodFiller.Bitmap != null && floodFiller.Bitmap.Bitmap != null)
                                            {
                                                image.Dispose();
                                                image = new Bitmap(floodFiller.Bitmap.Bitmap);
                                            }
                                        }
                                    }
                                    #endregion

                                    #region trimming
                                    if (Конструктор != null && Конструктор.ОбрезатьПустоеМесто)
                                    {
                                        // Find the min/max non-white/transparent pixels
                                        var min = new Point(int.MaxValue, int.MaxValue);
                                        var max = new Point(int.MinValue, int.MinValue);


                                        var isAlpha = image.PixelFormat.HasFlag(PixelFormat.Alpha);
                                        for (int x = 0; x < image.Width; ++x)
                                        {
                                            for (int y = 0; y < image.Height; ++y)
                                            {
                                                var pixelColor = image.GetPixel(x, y);

                                                if ((pixelColor.R < 245 && pixelColor.G < 245 && pixelColor.B < 245 && pixelColor.A > 0)
                                                    || (isAlpha && pixelColor.A > 0))
                                                {
                                                    if (x < min.X) min.X = x;
                                                    if (y < min.Y) min.Y = y;

                                                    if (x > max.X) max.X = x;
                                                    if (y > max.Y) max.Y = y;
                                                }
                                            }
                                        }

                                        var cropRectangle = new Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y);
                                        var newBitmap = new Bitmap(cropRectangle.Width, cropRectangle.Height);
                                        newBitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                                        var g = Graphics.FromImage(newBitmap);
                                        g.DrawImage(image, 0, 0, cropRectangle, GraphicsUnit.Pixel);
                                        g.Flush();

                                        image.Dispose();
                                        image = newBitmap;
                                    }
                                    #endregion

                                    var newSize = new Size(width, height);
                                    switch (type)
                                    {
                                        case ImageType.Full:
                                            {
                                                newSize = new Size(width, height);
                                                imgOutput = new Bitmap(newSize.Width, newSize.Height, PixelFormat.Format32bppArgb);
                                                canvas = Graphics.FromImage(imgOutput);
                                                canvas.SmoothingMode = SmoothingMode.Default;
                                                canvas.CompositingQuality = CompositingQuality.HighQuality;
                                                canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                                if (format == ImageFormat.Jpg)
                                                {
                                                    canvas.Clear(Color.White);
                                                }
                                                canvas.DrawImage(image, 0, 0, newSize.Width, newSize.Height);
                                                canvas.Flush();
                                            }
                                            break;

                                        case ImageType.Thumbnail:
                                            {
                                                int _width = Math.Min(image.Size.Height, image.Size.Width);
                                                imgOutput = new Bitmap(newSize.Width, newSize.Height, PixelFormat.Format32bppArgb);
                                                canvas = Graphics.FromImage(imgOutput);
                                                canvas.SmoothingMode = SmoothingMode.Default;
                                                canvas.CompositingQuality = CompositingQuality.HighQuality;
                                                canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                                if (format == ImageFormat.Jpg)
                                                {
                                                    canvas.Clear(Color.White);
                                                }
                                                canvas.DrawImage(image, new Rectangle(0, 0, newSize.Width, newSize.Height), new Rectangle(0, 0, _width, _width), GraphicsUnit.Pixel);
                                                canvas.Flush();
                                            }
                                            break;

                                        case ImageType.Resize:
                                            {
                                                if (newSize.Width > 0 && newSize.Height == 0)            // Сжатие по ширине (принудительное)
                                                {
                                                    newSize.Height = (int)(((double)newSize.Width / (double)image.Size.Width) * image.Size.Height);
                                                }
                                                else if (newSize.Width == 0 && newSize.Height > 0)      // Сжатие по высоте (принудительное)
                                                {
                                                    newSize.Width = (int)(((double)newSize.Height / (double)image.Size.Height) * image.Size.Width);
                                                }
                                                else
                                                {
                                                    int sourceWidth = image.Size.Width;
                                                    int sourceHeight = image.Size.Height;

                                                    float nPercent = 0;
                                                    float nPercentW = 0;
                                                    float nPercentH = 0;

                                                    nPercentW = ((float)newSize.Width / (float)sourceWidth);
                                                    nPercentH = ((float)newSize.Height / (float)sourceHeight);

                                                    if (nPercentH < nPercentW)
                                                        nPercent = nPercentH;
                                                    else
                                                        nPercent = nPercentW;

                                                    newSize = new Size((int)(sourceWidth * nPercent), (int)(sourceHeight * nPercent));
                                                }

                                                imgOutput = new Bitmap(newSize.Width, newSize.Height, PixelFormat.Format32bppArgb);
                                                canvas = Graphics.FromImage(imgOutput);
                                                canvas.SmoothingMode = SmoothingMode.Default;
                                                canvas.CompositingQuality = CompositingQuality.HighQuality;
                                                canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                                if (format == ImageFormat.Jpg)
                                                {
                                                    canvas.Clear(Color.White);
                                                }
                                                canvas.DrawImage(image, 0, 0, newSize.Width, newSize.Height);
                                                canvas.Flush();
                                            }
                                            break;
                                    }

                                    #region Добавить слои из конструктора
                                    if (Конструктор != null && Конструктор.Слои != null && canvas != null)
                                    {
                                        foreach (var item in Конструктор.Слои)
                                        {
                                            if (item.id_file == null || item.id_file.Equals(0m)) continue;

                                            using (var mcLayer = new MemoryStream(ПолучитьФайлПолностью(item.id_file, Хранилище.Оперативное, domain)))
                                            using (var imageLayer = new Bitmap(mcLayer))
                                            {
                                                canvas.DrawImage(imageLayer, item.x, item.y,
                                                    Convert.ToInt32(imageLayer.Width * (item.width > 0 ? item.width : 1.0)),
                                                    Convert.ToInt32(imageLayer.Height * (item.height > 0 ? item.height : 1.0)));
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            }
                            else
                            {
                                imgOutput = new Bitmap(width, height);
                                canvas = Graphics.FromImage(imgOutput);
                                canvas.Clear(Color.White);
                                canvas.DrawRectangle(Pens.Gray, 0, 0, width - 1, height - 1);
                                canvas.DrawString(
                                    "Not Found " + data.ПолучитьЗначение<string>(id_file, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                                    new Font("Tahoma", 8),
                                    Brushes.Gray,
                                    new RectangleF(0, 0, width, height),
                                    new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                            }
                        }
                        catch(Exception ex)
                        {
                            imgOutput = new Bitmap(width, height);
                            canvas = Graphics.FromImage(imgOutput);
                            canvas.Clear(Color.White);
                            canvas.DrawRectangle(Pens.Gray, 0, 0, width - 1, height - 1);
                            canvas.DrawString(
                                data.ПолучитьЗначение<string>(id_file, "НазваниеОбъекта", Хранилище.Оперативное, domain),
                                new Font("Tahoma", 8),
                                Brushes.Gray,
                                new RectangleF(0, 0, width, height),
                                new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                            ConfigurationClient.WindowsLog("FileService", string.Empty, domain, ex.ToString());
                        }

                        #region Вывод
                        switch (format)
                        {
                            case ImageFormat.Jpg:
                                {
                                    var ep = new EncoderParameters(1);
                                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Качество);
                                    ImageCodecInfo imageCodecInfo = null;
                                    foreach (ImageCodecInfo item in ImageCodecInfo.GetImageEncoders())
                                    {
                                        if (item.MimeType == "image/jpeg")
                                        {
                                            imageCodecInfo = item;
                                            break;
                                        }
                                    }
                                    imgOutput.Save(output, imageCodecInfo, ep);
                                }
                                break;

                            case ImageFormat.Png:
                                {
                                    imgOutput.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                                }
                                break;
                        }

                        //сохранить файл
                        System.IO.File.WriteAllBytes(full_filename, output.ToArray());
                        #endregion

                        return output.ToArray();
                    }
                }
                finally
                {
                    if (canvas != null) 
                        canvas.Dispose();
                    if (imgOutput != null) 
                        imgOutput.Dispose();
                }
            }
        }
        public byte[] ПолучитьФайлПросмотрПоНазванию(object id_node, string ИмяФайла, ImageType type, int width, int height, ImageFormat format, long Качество, КонструкторИзображения Конструктор, Хранилище хранилище, string domain)
        {
            //var query = new Query();
            //query.КоличествоВыводимыхДанных = 1;
            //query.КоличествоВыводимыхСтраниц = 1;
            //query.Типы.Add("Файл%");
            //query.УсловияПоиска.Add(new Query.УсловиеПоиска() { Атрибут = "ИдентификаторОбъекта", Значение = ИмяФайла });
            //query.ДобавитьВыводимыеКолонки(new string[] { "ИдентификаторОбъекта" });
            //query.МестаПоиска.Add(new Query.МестоПоиска() { id_node = id_node, МаксимальнаяГлубина = 1 });
            //var file = new DataClient().Поиск(query, хранилище, domain).AsEnumerable().SingleOrDefault();

            var file = new DataClient()._СписокФайлов(id_node, хранилище, domain).FirstOrDefault(p => p.ИдентификаторФайла == ИмяФайла);
            if (file == null) return null;
            return ПолучитьФайлПросмотр(file.id_node, type, width, height, format, Качество, Конструктор, domain);
        }
        #endregion

        #region Отчеты
        public ОтчетResponce Отчет(ОтчетRequest request)
        {
            try
            {
                var file = new ОтчетResponce();
                var buffer = new RosService.Helper.Отчет().СформироватьОтчет(request.НазваниеОтчета,
                    request.Параметры, request.ФорматОтчета,
                    Хранилище.Оперативное, request.user, request.domain);
                if (buffer != null)
                {
                    file.FileName = buffer.Name;
                    file.MimeType = buffer.MimeType;

                    if (buffer.Stream == null)
                        buffer.Stream = new byte[0];

                    file.FileByteStream = new MemoryStream(buffer.Stream);
                    file.Length = buffer.Stream.Length;
                }
                return file;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("ОтчетКонструктор", request.user, request.domain, ex.ToString());
            }
            return null;
        }
        public ОтчетResponce ОтчетКонструктор(ОтчетКонструкторRequest request)
        {
            try
            {
                var file = new ОтчетResponce();
                var buffer = new RosService.Helper.Отчет().СформироватьОтчет(request.Шаблон,
                    (Хранилище)Enum.Parse(typeof(Хранилище), request.Хранилище),
                    request.user, request.domain);
                if (buffer != null)
                {
                    file.FileName = buffer.Name;
                    file.MimeType = buffer.MimeType;

                    if (buffer.Stream == null)
                        buffer.Stream = new byte[0];

                    file.FileByteStream = new MemoryStream(buffer.Stream);
                    file.Length = buffer.Stream.Length;
                }
                return file;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("ОтчетКонструктор", request.user, request.domain, ex.ToString());
            }
            return null;
        }
        public DownloadResponce ОтчетДанные(ОтчетДанныеRequest request)
        {
            var id_report = new RosService.Helper.Отчет().FindReport(request.НазваниеОтчета, request.domain);
            var Запросы = new RosService.Helper.Отчет().BuildQuery(request.Параметры, id_report, request.domain);
            var ds = new RosService.Helper.Отчет().BuildDataSet(request.НазваниеОтчета, Запросы, Хранилище.Оперативное, request.user, request.domain);
            var xml = ds.GetXml();

            return new DownloadResponce()
            {
                FileName = request.НазваниеОтчета + ".xml",
                Length = xml.Length,
                FileByteStream = new MemoryStream(System.Text.Encoding.Default.GetBytes(xml))
            };
        }
        #endregion

        public string GetMimeType(string fileName)
        {
            var mimeType = "";
            var ext = System.IO.Path.GetExtension(fileName).ToLower();
            var regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (regKey != null && regKey.GetValue("Content Type") != null)
            {
                mimeType = regKey.GetValue("Content Type").ToString();
            }
            return mimeType;
        }
    }

}
