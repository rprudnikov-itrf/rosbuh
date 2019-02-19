using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace RosService.Files
{
    public partial class FileClient
    {
        public byte[] ПолучитьФайлПросмотр(decimal id_file, ImageType type, int width, int height, ImageFormat format, long Качество)
        {
            return ПолучитьФайлПросмотр(id_file, type, width, height, format, Качество, null, Client.Domain);
        }
        public byte[] ПолучитьФайлПросмотр(decimal id_file, ImageType type, int width, int height)
        {
            return ПолучитьФайлПросмотр(id_file, type, width, height, ImageFormat.Jpg, 80L, null, Client.Domain);
        }

        public byte[] ПолучитьФайлПросмотр(object id_file, ImageType type, int width, int height, ImageFormat format, long Качество, КонструкторИзображения Конструктор)
        {
            return ПолучитьФайлПросмотр(id_file, type, width, height, format, Качество, Конструктор, Client.Domain);
        }

        public RosService.Data.Файл Отчет(string НазваниеОтчета, QueryПараметр[] Параметры, RosService.Files.ФорматОтчета ФорматОтчета)
        {
            var FileStream = null as Stream;
            var FileLength = 0L;
            var FileType = RosService.Files.MimeType.НеОпределен;
            var FileName = Отчет(НазваниеОтчета, Параметры, ФорматОтчета, RosService.Client.UserName, RosService.Client.Domain,
                out FileLength, out FileType, out FileStream);
            
            //загрузка файла
            var file = new byte[FileLength];
            var chunkSize = 1024 * 64;
            var buffer = new byte[chunkSize];
            var bytesRead = 0;
            using (var ms = new MemoryStream(file))
            {
                while ((bytesRead = FileStream.Read(buffer, 0, chunkSize)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
                return new RosService.Data.Файл() { Stream = ms.ToArray(), Name = FileName };
            }
        }
    }

    public class File : FileStream
    {
        private long bytesRead = 0;
        private FileInfo fileInfo { get; set; }

        public class ProgressChangedEventArgs : EventArgs
        {
            public long BytesRead;
            public long Length;

            public ProgressChangedEventArgs(long BytesRead, long Length)
            {
                this.BytesRead = BytesRead;
                this.Length = Length;
            }
        }
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        public new string Name
        {
            get
            {
                return fileInfo.Name;
            }
        }

        public File(string path, FileMode mode, FileAccess access) :
            base(path, mode, access)
        {
            this.fileInfo = new FileInfo(path);
            if (ProgressChanged != null) ProgressChanged(this, new ProgressChangedEventArgs(bytesRead, Length));
        }

        public File(string path, long length) :
            base(path, FileMode.Create, FileAccess.ReadWrite)
        {
            base.SetLength(length);

            this.fileInfo = new FileInfo(path);
            if (ProgressChanged != null) ProgressChanged(this, new ProgressChangedEventArgs(bytesRead, Length));
        }

        public double GetProgress()
        {
            return ((double)bytesRead) / Length;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { 
                return true; 
            }
        }

        public override bool CanWrite
        {
            get { 
                return true; 
            }
        }

        public override void Flush() { }

        public override long Length
        {
            get { return fileInfo.Length; }
        }

        public override long Position
        {
            get { return bytesRead; }
            set { throw new Exception("The method or operation is not implemented."); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return base.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            base.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = base.Read(buffer, offset, count);
            bytesRead += result;
            if (ProgressChanged != null) ProgressChanged(this, new ProgressChangedEventArgs(bytesRead, Length));
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            bytesRead += count;
            if (ProgressChanged != null) ProgressChanged(this, new ProgressChangedEventArgs(bytesRead, Length));
        }
    }
}
