/// Notice : Code written by Dimitris Papadimitriou - http://www.papadi.gr
/// Code is provided to be used freely but without any warranty of any kind
using System;
using System.ServiceModel;
using System.Runtime.Serialization;
using RosService.Data;
using RosService.Files;

namespace RosService.Intreface
{
    [ServiceContract]
    public interface IFile
    {
        [OperationContract]
        DownloadResponce ПолучитьФайл(DownloadRequest request);
        [OperationContract]
        byte[] ПолучитьФайлПолностью(object id_file, Хранилище хранилище, string domain);
        [OperationContract]
        byte[] ПолучитьФайлПолностьюПоНазванию(object id_node, string ИдентификаторФайла, Хранилище хранилище, string domain);

        [OperationContract]
        UploadResponce СохранитьФайл(UploadRequest request);
        [OperationContract]
        decimal СохранитьФайлПолностью(object id_node, string ИдентификаторФайла, string ИмяФайла, string Описание, byte[] stream, Хранилище хранилище, string user, string domain);
        [OperationContract(IsOneWay = true)]
        void СохранитьФайлПолностьюАсинхронно(object id_node, string ИдентификаторФайла, string ИмяФайла, string Описание, byte[] stream, Хранилище хранилище, string user, string domain);

        [OperationContract]
        ОтчетResponce Отчет(ОтчетRequest request);
        [OperationContract]
        ОтчетResponce ОтчетКонструктор(ОтчетКонструкторRequest request);
        [OperationContract]
        DownloadResponce ОтчетДанные(ОтчетДанныеRequest request);

        [OperationContract]
        byte[] ПолучитьФайлПросмотр(object id_file, ImageType type, int width, int height, ImageFormat format, long Качество, КонструкторИзображения Конструктор, string domain);
        [OperationContract]
        byte[] ПолучитьФайлПросмотрПоНазванию(object id_node, string ИмяФайла, ImageType type, int width, int height, ImageFormat format, long Качество, КонструкторИзображения Конструктор, Хранилище хранилище, string domain);

        [OperationContract]
        string GetMimeType(string fileName);
    }
}
namespace RosService.Files
{
    [MessageContract]
    public class DownloadRequest
    {
        [MessageBodyMember]
        public decimal id_file;

        [MessageHeader(MustUnderstand = true)]
        public Хранилище хранилище;

        [MessageHeader(MustUnderstand = true)]
        public string user;

        [MessageHeader(MustUnderstand = true)]
        public string domain;
    }

    [MessageContract]
    public class DownloadResponce : IDisposable
    {
        [MessageHeader(MustUnderstand = true)]
        public string FileName;

        [MessageHeader(MustUnderstand = true)]
        public long Length;

        [MessageBodyMember(Order = 1)]
        public System.IO.Stream FileByteStream;

        public void Dispose()
        {
            try
            {
                if (FileByteStream != null)
                {
                    FileByteStream.Close();
                    FileByteStream = null;
                }
            }
            catch
            {
            }
        }
    }


    [MessageContract]
    public class UploadRequest : IDisposable
    {
        [MessageBodyMember(Order = 1)]
        public System.IO.Stream FileByteStream;
        
        [MessageHeader(MustUnderstand = true)]
        public decimal id_node;

        [MessageHeader(MustUnderstand = true)]
        public string ИдентификаторФайла;

        [MessageHeader(MustUnderstand = true)]
        public string ИмяФайла;

        [MessageHeader(MustUnderstand = true)]
        public string Описание;

        [MessageHeader(MustUnderstand = true)]
        public long Length;

        [MessageHeader(MustUnderstand = true)]
        public Хранилище хранилище;

        [MessageHeader(MustUnderstand = true)]
        public string user;

        [MessageHeader(MustUnderstand = true)]
        public string domain;

        public void Dispose()
        {
            try
            {
                if (FileByteStream != null)
                {
                    FileByteStream.Close();
                    FileByteStream = null;
                }
            }
            catch
            {
            }
        }
    }
    [MessageContract]
    public class UploadResponce
    {
        [MessageBodyMember]
        public decimal id_file;
    }

    [MessageContract]
    public class ОтчетRequest
    {
        [MessageBodyMember(Order = 1)]
        public string НазваниеОтчета;
        [MessageBodyMember(Order = 2)]
        public Query.Параметр[] Параметры;
        [MessageBodyMember(Order = 3)]
        public ФорматОтчета ФорматОтчета;
        [MessageBodyMember(Order = 4)]
        public string user;
        [MessageBodyMember(Order = 5)]
        public string domain;
    }
    [MessageContract]
    public class ОтчетДанныеRequest
    {
        [MessageBodyMember(Order = 1)]
        public string НазваниеОтчета;
        [MessageBodyMember(Order = 2)]
        public Query.Параметр[] Параметры;
        [MessageBodyMember(Order = 3)]
        public string user;
        [MessageBodyMember(Order = 4)]
        public string domain;
    }
    [MessageContract]
    public class ОтчетКонструкторRequest
    {
        [MessageBodyMember(Order = 0)]
        public ШаблонОтчета Шаблон;
        [MessageBodyMember(Order = 1)]
        public string Хранилище;
        [MessageBodyMember(Order = 2)]
        public string user;
        [MessageBodyMember(Order = 3)]
        public string domain;
    }
    [MessageContract]
    public class ОтчетResponce : IDisposable
    {
        [MessageHeader(MustUnderstand = true)]
        public string FileName;

        [MessageHeader(MustUnderstand = true)]
        public long Length;
        
        [MessageHeader(MustUnderstand = true)]
        public MimeType MimeType;

        [MessageBodyMember(Order = 1)]
        public System.IO.Stream FileByteStream;

        public void Dispose()
        {
            try
            {
                if (FileByteStream != null)
                {
                    FileByteStream.Close();
                    FileByteStream = null;
                }
            }
            catch
            {
            }
        }
    }

    public enum ImageType
    {
        Full = 0,
        Thumbnail,
        Resize
    }
    public enum ImageFormat
    {
        Jpg,
        Png
    }

    [DataContract]
    public class ШаблонОтчета
    {
        [DataContract]
        public enum ТипЗначения
        {
            [EnumMember]
            String,
            [EnumMember]
            DateTime,
            [EnumMember]
            Number
        }
        [DataContract]
        public enum Ориентация
        {
            [EnumMember]
            Альбом,
            [EnumMember]
            Книга
        }
        [DataContract]
        public class Колонка
        {
            [DataMember]
            public string Название { get; set; }
            [DataMember]
            public string Атрибут { get; set; }

            [DataMember(IsRequired = true)]
            public double Размер { get; set; }
            [DataMember(IsRequired = true)]
            public ТипЗначения ТипЗначения { get; set; }
            [DataMember]
            public string Формат { get; set; }
        };

        [DataMember]
        public string НазваниеОтчета { get; set; }
        [DataMember(IsRequired = true)]
        public Ориентация ОриентацияСтраницы { get; set; }
        [DataMember(IsRequired = true)]
        public ФорматОтчета ФорматОтчета { get; set; }
        [DataMember(IsRequired = true)]
        public Колонка[] Колонки { get; set; }
        [DataMember(IsRequired = true)]
        public string[] Запросы { get; set; }
        [DataMember(IsRequired = true)]
        public string[] Группировки { get; set; }
        [DataMember(IsRequired = true)]
        public bool ВерхнийКолонтитул { get; set; }
        [DataMember(IsRequired = true)]
        public bool НижнийКолонтитул { get; set; }
    };


    [DataContract]
    public enum HorizontalAlign
    {
        [EnumMember]
        Left,
        [EnumMember]
        Right,
        [EnumMember]
        Center
    };
    [DataContract]
    public enum VerticalAlign
    {
        [EnumMember]
        Top,
        [EnumMember]
        Bottom,
        [EnumMember]
        Center
    };
    [DataContract]
    public class СлойИзображения
    {
        [DataMember(IsRequired = true)]
        public object id_file { get; set; }

        [DataMember(IsRequired = true)]
        public int x { get; set; }
        [DataMember(IsRequired = true)]
        public int y { get; set; }

        [DataMember(IsRequired = true)]
        public double width { get; set; }
        [DataMember(IsRequired = true)]
        public double height { get; set; }
        [DataMember(IsRequired = true)]
        public HorizontalAlign HorizontalAlign { get; set; }
        [DataMember(IsRequired = true)]
        public VerticalAlign VerticalAlign { get; set; }
    };

    [DataContract]
    public class КонструкторИзображения
    {
        [DataMember(IsRequired = true)]
        public СлойИзображения[] Слои { get; set; }
        [DataMember(IsRequired = true)]
        public bool ОбрезатьПустоеМесто { get; set; }
        [DataMember(IsRequired = true)]
        public bool ПрозрачныйФон { get; set; }
    };
}
