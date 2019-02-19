using System;
using System.ServiceModel;
using RosService.Configuration;
using RosService.Data;
using RosService.Finance;
using System.ComponentModel;
using System.Data;
using System.Linq;
using RosService.Users;
using System.Collections.Generic;
using RosService.Services;
using RosService.Files;
using System.ServiceModel.Channels;
using RosService.ServiceModel;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Threading;
using System.ServiceModel.Description;
using System.Text;
using System.IO.Compression;
using System.IO;
using System.Diagnostics;

namespace RosService
{
    /// <summary>
    /// Режим работы веб-сервиса
    /// </summary>
    public enum ClientMode
    {
        None,

        /// <summary>
        /// basicHttpBinding без поддержки транзакций
        /// адрес сервера можно изменить с помощью настройки RosService.Url в файле web.config
        /// </summary>
        Http,
        WSHttp,

        //HttpBinary,

        /// <summary>
        /// С поддержкой TransactionScope
        /// </summary>
        Server,

        Tcp,
        NamedPipe,
        GZip
    };

    public class Client : IDisposable
    {
        private class ClientHost : IDisposable
        {
            public readonly object lockObj = new object();
            public DateTime IdleTimeout;
            public IEnumerable<ICommunicationObject> Items;
            public DataClient Архив;
            public ConfigurationClient Конфигуратор;
            public FinanceClient Бухгалтерия;
            public ServicesClient Сервисы;
            public FileClient Файлы;

            public ClientHost()
            {
            }



            public bool IsInitialize { get; private set; }
            public void Initialize()
            {
                if (IsFaulted(Архив))
                {
                    //var s = System.Diagnostics.Stopwatch.StartNew();
                    //if(DataF == null)
                    //    DataF = new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Data"));

                    Архив = new DataClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Data")));
                    Архив.InnerChannel.Faulted += new EventHandler(InnerChannel_Faulted);
                    Архив.InnerChannel.Closing += new EventHandler(InnerChannel_Closing);
                    //Архив.Open();
                }

                if (IsFaulted(Конфигуратор))
                {
                    Конфигуратор = new ConfigurationClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration")));
                    Конфигуратор.InnerChannel.Faulted += new EventHandler(InnerChannel_Faulted);
                    Конфигуратор.InnerChannel.Closing += new EventHandler(InnerChannel_Closing);

                    // Find the ContractDescription of the operation to find.
                    //ContractDescription cd = ((ConfigurationClient)Конфигуратор).Endpoint.op
                    //OperationDescription myOperationDescription = cd.Operations.Find("Add");

                    //// Find the serializer behavior.
                    //DataContractSerializerOperationBehavior serializerBehavior =
                    //    myOperationDescription.Behaviors.
                    //       Find<DataContractSerializerOperationBehavior>();

                    // If the serializer is not found, create one and add it.
                    //if (serializerBehavior == null)
                    //{
                    //    serializerBehavior = new DataContractSerializerOperationBehavior(myOperationDescription);
                    //    myOperationDescription.Behaviors.Add(serializerBehavior);
                    //}
                    //var serializerBehavior = new DataContractSerializerOperationBehavior(new OperationDescription("f1", new ContractDescription("f1"));
                    //serializerBehavior.MaxItemsInObjectGraph = int.MaxValue;
                    //Конфигуратор.Endpoint.Behaviors.Add(
                    //var f = Конфигуратор.Endpoint.Behaviors.Find<DataContractSerializerOperationBehavior>();
                    //serializerBehavior.IgnoreExtensionDataObject = true;
                }

                if (IsFaulted(Бухгалтерия))
                {
                    Бухгалтерия = new FinanceClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Finance")));
                    Бухгалтерия.InnerChannel.Faulted += new EventHandler(InnerChannel_Faulted);
                    Бухгалтерия.InnerChannel.Closing += new EventHandler(InnerChannel_Closing);
                }

                if (IsFaulted(Сервисы))
                {
                    Сервисы = new ServicesClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Services")));
                    Сервисы.InnerChannel.Faulted += new EventHandler(InnerChannel_Faulted);
                    Сервисы.InnerChannel.Closing += new EventHandler(InnerChannel_Closing);
                }

                if (IsFaulted(Файлы))
                {
                    Файлы = new FileClient(DefaultStreamingBinding, new EndpointAddress(string.Format(RosService.Client.DefaultStreamingEndpointAddress, "File")));
                    Файлы.InnerChannel.Faulted += new EventHandler(InnerChannel_Faulted);
                    Файлы.InnerChannel.Closing += new EventHandler(InnerChannel_Closing);
                }
                Items = new ICommunicationObject[] { Архив, Конфигуратор, Бухгалтерия, Сервисы, Файлы };
                IsInitialize = true;
            }
            void InnerChannel_Closing(object sender, EventArgs e)
            {
                IsInitialize = false;
            }
            void InnerChannel_Faulted(object sender, EventArgs e)
            {
                IsInitialize = false;
            }
            public void Dispose()
            {
                if (Items == null)
                    return;

                foreach (var item in Items.ToArray())
                {
                    if (item == null)
                        continue;
                    
                    try
                    {
                        if (item.State == CommunicationState.Faulted)
                            item.Abort();
                        else if (item.State != CommunicationState.Closed)
                            item.Close();
                    }
                    catch (CommunicationException)
                    {
                        item.Abort();
                    }
                    catch (TimeoutException)
                    {
                        item.Abort();
                    }
                    catch (Exception)
                    {
                    }
                }
                Items = null;
                IsInitialize = false;
            }

            private bool IsFaulted(ICommunicationObject item)
            {
                if (item == null)
                    return true;

                switch (item.State)
                {
                    case CommunicationState.Faulted:
                    case CommunicationState.Closed:
                    case CommunicationState.Closing:
                        return true;
                }
                return false;
            }
        }

        private IEnumerable<ICommunicationObject> CommunicationObjects;

        public static readonly string DefaultHost = "itrf.ru";
        //public static readonly string DefaultHost = "89.108.116.232";
        public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(11);
        public static readonly string DefaultPort = "8080";

        public static bool IsAuthorization { get; private set; }
        public static string UserName { get; private set; }
        public static string Password { get; private set; }
        public static string Domain { get; private set; }
        public static UserClient User { get; private set; }
        public static System.ServiceModel.Channels.Binding DefaultBinding { get; private set; }
        public static System.ServiceModel.Channels.Binding DefaultStreamingBinding { get; private set; }
        public static string DefaultEndpointAddress { get; private set; }
        public static string DefaultStreamingEndpointAddress { get; private set; }

        #region event
        public static EventHandler<AddClientEventArgs> ПередДобавлением;
        public static EventHandler<AddClientPostEventArgs> ПослеДобавления;
        #endregion

        public string Домен
        {
            get
            {
                return Domain;
            }
        }
        public string Пользователь
        {
            get
            {
                return UserName;
            }
        }
        public UserClient СведенияПользователя
        {
            get
            {
                return User;
            }
        }

        private static string host;
        public static string Host
        {
            get
            {
                return host;
            }
            set
            {
                if (host != value)
                {
                    host = value;
                    UpdateHost();
                }
            }
        }

        //private static int version;
        //public static int Version
        //{
        //    get
        //    {
        //        return version;
        //    }
        //    set
        //    {
        //        if (version != value)
        //        {
        //            version = value;
        //            UpdateHost();
        //        }
        //    }
        //}

        public static string GetPath()
        {
            return "/RosService";
            //if (Version < 5)
            //    return "/RosService";
            //else
            //return "/RosService" + Version.ToString();
        }
        public static string GetUrl(string host, ClientMode mode)
        {
            var token = host.Split(':');
            var url = token.ElementAtOrDefault(0);
            var port = token.ElementAtOrDefault(1);
            if (string.IsNullOrEmpty(port)) port = DefaultPort;

            switch (Mode)
            {
                case ClientMode.Http:
                    return "http://" + url + ":" + port + GetPath() + @"/{0}/basic";

                case ClientMode.WSHttp:
                    return "http://" + url + ":" + port + GetPath() + @"/{0}";

                //case ClientMode.Server:
                //    return "http://" + host + ":" + port + @"/RosService/{0}/binary";

                case ClientMode.GZip:
                    return "http://" + url + ":" + port + GetPath() + @"/{0}/gzip";

                case ClientMode.Tcp:
                case ClientMode.Server:
                    return "net.tcp://" + url + GetPath() + @"/{0}/tcp";

                case ClientMode.NamedPipe:
                    return "net.pipe://" + url + GetPath() + @"/{0}/pipe";
            }
            return null;
        }
        public static string GetUrlStreaming(string host, ClientMode mode)
        {
            var token = host.Split(':');
            var url = token.ElementAtOrDefault(0);
            var port = token.ElementAtOrDefault(1);
            if (string.IsNullOrEmpty(port)) port = DefaultPort;

            return "http://" + url + ":" + port + GetPath() + @"/{0}/binary";
        }
        protected static void UpdateHost()
        {
            DisposeObject();

            //if ("itrf.ru".Equals(host))
            //    host = DefaultHost;

            var str = !string.IsNullOrEmpty(host) ? host : DefaultHost;
            DefaultEndpointAddress = GetUrl(str, Mode);
            DefaultStreamingEndpointAddress = GetUrlStreaming(str, Mode);
        }

        private static ClientMode _mode;
        public static ClientMode Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                if (_mode != value)
                {
                    _mode = value;


                    if (!string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["RosService.MaxPoolSize"]))
                    {
                        maxService = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["RosService.MaxPoolSize"]);
                    }
                    else
                    {
                        switch (value)
                        {
                            case ClientMode.Server:
                            case ClientMode.Tcp:
                            case ClientMode.NamedPipe:
                                maxService = 10;
                                break;

                            default:
                                maxService = 10;
                                break;
                        }
                    }

                    #region set
                    switch (value)
                    {
                        case ClientMode.Http:
                            {
                                DefaultBinding = new BasicHttpBinding()
                                {
                                    SendTimeout = TimeSpan.FromSeconds(30),
                                    ReceiveTimeout = TimeSpan.FromSeconds(60),
                                    MaxReceivedMessageSize = int.MaxValue,
                                    MaxBufferSize = int.MaxValue,
                                    //MaxBufferPoolSize = 65536,
                                    TextEncoding = System.Text.Encoding.UTF8,
                                    TransferMode = TransferMode.Buffered,
                                    MessageEncoding = WSMessageEncoding.Text,
                                    UseDefaultWebProxy = true,
                                    ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas()
                                    {
                                        MaxStringContentLength = int.MaxValue,
                                        MaxArrayLength = int.MaxValue,
                                        //MaxBytesPerRead = 65536
                                    }
                                };
                            }
                            break;

                        case ClientMode.WSHttp:
                            {
                                var _WSHttpBinding = new WSHttpBinding()
                                {
                                    //TransactionFlow = true,
                                    TransactionFlow = false,
                                    SendTimeout = TimeSpan.FromMinutes(10),
                                    ReceiveTimeout = TimeSpan.FromMinutes(10),
                                    MaxReceivedMessageSize = int.MaxValue,
                                    MessageEncoding = WSMessageEncoding.Text,
                                    TextEncoding = System.Text.Encoding.UTF8,
                                    UseDefaultWebProxy = true,
                                    ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas()
                                    {
                                        MaxStringContentLength = int.MaxValue,
                                        MaxArrayLength = int.MaxValue,
                                        //MaxBytesPerRead = 65536
                                    }
                                };

                                _WSHttpBinding.Security.Mode = SecurityMode.None;
                                _WSHttpBinding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
                                _WSHttpBinding.Security.Message.NegotiateServiceCredential = true;
                                _WSHttpBinding.Security.Message.EstablishSecurityContext = true;
                                _WSHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                                _WSHttpBinding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
                                _WSHttpBinding.Security.Transport.Realm = "";
                                DefaultBinding = _WSHttpBinding;
                            }
                            break;

                        #region case ClientMode.Server
                        //case ClientMode.Server:
                        //    {
                        //        var _CustomBinding = new CustomBinding()
                        //        {
                        //            SendTimeout = TimeSpan.FromSeconds(30),
                        //            ReceiveTimeout = TimeSpan.FromSeconds(60)
                        //        };
                        //        var _BinaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement()
                        //        {
                        //            MaxReadPoolSize = int.MaxValue,
                        //            MaxWritePoolSize = int.MaxValue
                        //        };
                        //        _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxStringContentLength = int.MaxValue;
                        //        _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxArrayLength = int.MaxValue;
                        //        _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
                        //        _CustomBinding.Elements.Add(_BinaryMessageEncodingBindingElement);
                        //        _CustomBinding.Elements.Add(new TransactionFlowBindingElement());
                        //        _CustomBinding.Elements.Add(new HttpTransportBindingElement()
                        //        {
                        //            MaxBufferPoolSize = int.MaxValue,
                        //            MaxBufferSize = int.MaxValue,
                        //            MaxReceivedMessageSize = int.MaxValue
                        //        });
                        //        DefaultBinding = _CustomBinding;
                        //    }
                        //    break;
                        #endregion

                        case ClientMode.GZip:
                            {
                                var _CustomBinding = new CustomBinding()
                                {
                                    SendTimeout = TimeSpan.FromSeconds(60),
                                    ReceiveTimeout = TimeSpan.FromSeconds(60)
                                };
                                var _BinaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement()
                                {
                                    MaxReadPoolSize = 65536, //1048576 //int.MaxValue,
                                    MaxWritePoolSize = 65536 //1048576, //int.MaxValue
                                };
                                _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxStringContentLength = int.MaxValue;
                                _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxArrayLength = int.MaxValue;
                                _BinaryMessageEncodingBindingElement.ReaderQuotas.MaxBytesPerRead = int.MaxValue;

                                var _GZipMessageEncodingBindingElement = new GZipMessageEncodingBindingElement()
                                {
                                    InnerMessageEncodingBindingElement = _BinaryMessageEncodingBindingElement
                                };

                                _CustomBinding.Elements.Add(_GZipMessageEncodingBindingElement);
                                _CustomBinding.Elements.Add(new HttpTransportBindingElement()
                                {
                                    UseDefaultWebProxy = true,
                                    //ProxyAddress = new Uri("http://202.29.60.220:8080"),
                                    MaxReceivedMessageSize = int.MaxValue,
                                    MaxBufferSize = int.MaxValue,
                                    //MaxBufferPoolSize = 65536
                                });
                                DefaultBinding = _CustomBinding;
                            }
                            break;

                        case ClientMode.Server:
                        case ClientMode.Tcp:
                            {
                                var _NetTcpBinding = new NetTcpBinding(SecurityMode.None, false)
                                {
                                    PortSharingEnabled = true,
                                    TransactionFlow = false,
                                    //TransactionFlow = true,
                                    MaxConnections = 100,
                                    SendTimeout = TimeSpan.FromMinutes(10),
                                    ReceiveTimeout = InactivityTimeout,
                                    MaxReceivedMessageSize = int.MaxValue,
                                    MaxBufferSize = int.MaxValue,
                                    //MaxBufferPoolSize = 65536, //int.MaxValue,
                                    TransferMode = TransferMode.Buffered,
                                    ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas()
                                    {
                                        MaxStringContentLength = int.MaxValue,
                                        MaxArrayLength = int.MaxValue,
                                        MaxBytesPerRead = int.MaxValue
                                    }
                                };
                                _NetTcpBinding.ReliableSession.Ordered = false;
                                _NetTcpBinding.ReliableSession.InactivityTimeout = InactivityTimeout;
                                DefaultBinding = _NetTcpBinding;
                            }
                            break;

                        case ClientMode.NamedPipe:
                            {
                                var _NetNamedPipeBinding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
                                {
                                    TransactionFlow = true,
                                    SendTimeout = TimeSpan.FromMinutes(10),
                                    ReceiveTimeout = InactivityTimeout,
                                    MaxReceivedMessageSize = int.MaxValue,
                                    MaxBufferSize = int.MaxValue,
                                    //MaxBufferPoolSize = 65536,
                                    MaxConnections = 100,
                                    TransferMode = TransferMode.Buffered,
                                    ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas()
                                    {
                                        MaxStringContentLength = int.MaxValue,
                                        MaxArrayLength = int.MaxValue,
                                        MaxBytesPerRead = int.MaxValue
                                    },
                                };
                                DefaultBinding = _NetNamedPipeBinding;
                            }
                            break;
                    }
                    #endregion

                    #region File
                    var binding = new CustomBinding()
                    {
                        SendTimeout = TimeSpan.FromMinutes(5),
                        ReceiveTimeout = TimeSpan.FromMinutes(5)
                    };
                    var _BinaryMessageEncodingBindingElement2 = new BinaryMessageEncodingBindingElement()
                    {
                        //MaxReadPoolSize = 65536,
                        //MaxWritePoolSize = 65536
                    };
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxStringContentLength = int.MaxValue;
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxArrayLength = int.MaxValue;
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
                    binding.Elements.Add(_BinaryMessageEncodingBindingElement2);
                    binding.Elements.Add(new HttpTransportBindingElement()
                    {
                        //MaxBufferPoolSize = 65536,
                        MaxBufferSize = int.MaxValue,
                        MaxReceivedMessageSize = int.MaxValue,
                        TransferMode = TransferMode.Streamed
                    });
                    #endregion
                    DefaultStreamingBinding = binding;
                    UpdateHost();
                }
            }
        }

        #region Создаем глобальные значения тк при их создании тратится 20 мс
        private static int _maxService;
        public static int maxService
        {
            get
            {
                return _maxService;
            }
            set
            {
                if (_maxService != value)
                {
                    //var list = new List<ClientHost>(value);
                    for (int i = 0; i < value; i++)
                        _ClientHosts.Add(new ClientHost());

                    //list.Add(new ClientHost());
                    //_ClientHosts = list.ToArray();

                    //if(value == 1)
                    //    _ClientHosts = new ClientHost[] { new ClientHost() };
                    //else
                    //    _ClientHosts = new ClientHost[] { new ClientHost(), new ClientHost(), new ClientHost() };

                    _maxService = value;
                }
            }
        }
        private static int currentService;

        private static ConcurrentBag<ClientHost> _ClientHosts = new ConcurrentBag<ClientHost>();
        private static readonly object lockobj = new object();
        private static void DisposeObject()
        {
            if (_ClientHosts == null)
                return;

            foreach (var item in _ClientHosts)
            {
                if (item != null)
                    item.Dispose();
            }
        }
        #endregion

        public readonly DataClient Архив;
        public readonly ConfigurationClient Конфигуратор;
        public readonly FinanceClient Бухгалтерия;
        public readonly ServicesClient Сервисы;
        public readonly FileClient Файлы;

        static Client()
        {
            User = new UserClient() { Права = new НаборПраваПользователя() };
            UserName = "";
            Password = "";
            Domain = "";

            //var ver = System.Configuration.ConfigurationManager.AppSettings["RosService.Version"];
            //if (!string.IsNullOrEmpty(ver))
            //{
            //    version = Convert.ToInt32(ver);
            //}
            if (Mode == ClientMode.None)
            {
                var mode = System.Configuration.ConfigurationManager.AppSettings["RosService.Mode"];
                if (!string.IsNullOrEmpty(mode))
                {
                    Mode = (ClientMode)Enum.Parse(typeof(ClientMode), mode);
                }
                else
                {
                    Mode = ClientMode.GZip;
                }
            }
            if (string.IsNullOrEmpty(Client.Host))
            {
                Client.Host = System.Configuration.ConfigurationManager.AppSettings["RosService.Url"] ?? DefaultHost;
            }

            #region увеличить число одновременных подключений
            switch (Mode)
            {
                case ClientMode.Server:
                case ClientMode.Tcp:
                case ClientMode.NamedPipe:
                    System.Net.ServicePointManager.DefaultConnectionLimit = 100;
                    break;

                case ClientMode.GZip:
                case ClientMode.Http:
                case ClientMode.WSHttp:
                default:
                    System.Net.ServicePointManager.DefaultConnectionLimit = 10;
                    break;
            }
            #endregion
        }
        public Client()
        {
            InitializeComponent();

            var current = null as ClientHost;
            do
            {
                if (currentService >= maxService)
                    currentService = 0;

                current = _ClientHosts.ElementAtOrDefault(currentService);
                Interlocked.Increment(ref currentService);
            } while (current == null);

            lock (current.lockObj)
            {
                #region NamePipe, Tcp сбрасывать соединение
                switch (Mode)
                {
                    case ClientMode.Server:
                    case ClientMode.Tcp:
                    case ClientMode.NamedPipe:
                        {
                            var _time = DateTime.Now;
                            if (current.IdleTimeout <= _time)
                            {
                                current.Dispose();
                                current.IdleTimeout = _time.Add(InactivityTimeout);
                            }
                        }
                        break;
                }
                #endregion

                if (!current.IsInitialize)
                {
                    current.Dispose();
                    current.Initialize();
                }

                this.Архив = current.Архив;
                this.Конфигуратор = current.Конфигуратор;
                this.Бухгалтерия = current.Бухгалтерия;
                this.Сервисы = current.Сервисы;
                this.Файлы = current.Файлы;
            }
        }
        public Client(TimeSpan timeout)
        {
            InitializeComponent();

            //для создания timeout создаем новые ссылки
            this.Архив = new DataClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Data")));
            this.Бухгалтерия = new FinanceClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Finance")));
            this.Конфигуратор = new ConfigurationClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration")));
            this.Сервисы = new ServicesClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Services")));
            this.Файлы = new FileClient(DefaultStreamingBinding, new EndpointAddress(string.Format(RosService.Client.DefaultStreamingEndpointAddress, "File")));

            CommunicationObjects = new ICommunicationObject[] { Архив, Конфигуратор, Бухгалтерия, Сервисы, Файлы };
            SetTimeout(timeout);
        }
        public void Dispose()
        {
            if (CommunicationObjects != null)
            {
                foreach (var item in CommunicationObjects.ToArray())
                {
                    try
                    {
                        if (item.State == CommunicationState.Faulted)
                            item.Abort();
                        else if (item.State != CommunicationState.Closed)
                            item.Close();
                    }
                    catch (CommunicationException)
                    {
                        item.Abort();
                    }
                    catch (TimeoutException)
                    {
                        item.Abort();
                    }
                    catch
                    {
                    }
                }
                CommunicationObjects = null;
            }
        }
        public void Abort()
        {
            if (CommunicationObjects != null)
            {
                foreach (var item in CommunicationObjects)
                {
                    if (item != null) item.Abort();
                }
            }
        }

        public void DownloadStringAsync(string url, params object[] args)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() => DownloadString(url, args));
        }
        public string DownloadString(string url, params object[] args)
        {
            try
            {
                using (var web = new System.Net.WebClient() { Encoding = Encoding.UTF8 })
                {
                    web.Headers["Accept-Encoding"] = "gzip";
                    var buffer = web.DownloadData(string.Format(url, args));
                    if (web.ResponseHeaders.AllKeys.Contains("Content-Encoding")
                        && "gzip".Contains((web.ResponseHeaders["Content-Encoding"] ?? "").ToLower()))
                    {
                        return DecompressGZip(buffer);
                    }
                    return System.Text.Encoding.UTF8.GetString(buffer);

                    //return web.DownloadString(string.Format(url, args));
                }
            }
            catch (Exception ex)
            {
                WindowsLog(ex);
            }

            return "";
        }

        public void DownloadStringAsync(string url)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() => DownloadString(url));
        }
        public string DownloadString(string url)
        {
            try
            {
                using (var web = new System.Net.WebClient() { Encoding = Encoding.UTF8 })
                {
                    web.Headers["Accept-Encoding"] = "gzip";
                    var buffer = web.DownloadData(url);
                    if (web.ResponseHeaders.AllKeys.Contains("Content-Encoding")
                        && "gzip".Contains((web.ResponseHeaders["Content-Encoding"] ?? "").ToLower()))
                    {
                        return DecompressGZip(buffer);
                    }
                    return System.Text.Encoding.UTF8.GetString(buffer);

                    //return web.DownloadString(url);
                }
            }
            catch (Exception ex)
            {
                WindowsLog(ex);
            }
            return "";
        }

        private static string DecompressGZip(byte[] gzip)
        {
            if (gzip.Length == 0)
                return "";

            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return System.Text.Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        protected void InitializeComponent()
        {
            if (!IsAuthorization)
            {
                lock (lockobj)
                {
                    //Произвести авторизацию из web.config
                    if (!IsAuthorization)
                    {
                        Authorization(
                            System.Configuration.ConfigurationManager.AppSettings["RosService.UserName"],
                            System.Configuration.ConfigurationManager.AppSettings["RosService.Password"],
                            false);
                    }
                }
            }
        }
        protected void SetTimeout(TimeSpan timeout)
        {
            if (CommunicationObjects != null)
            {
                foreach (var item in CommunicationObjects.ToArray())
                {
                    if (item == null) continue;

                    var property = item.GetType().GetProperty("Endpoint");
                    if (property != null)
                    {
                        var Endpoint = property.GetValue(item, null) as System.ServiceModel.Description.ServiceEndpoint;
                        if (Endpoint != null)
                        {
                            Endpoint.Binding.SendTimeout = timeout;
                            Endpoint.Binding.ReceiveTimeout = timeout;
                        }
                    }
                }
            }
        }
        protected static void SetAuthorization(bool value)
        {
            IsAuthorization = value;
        }

        #region Авторизация
        //http://itrf.ru/росинфотех/техподдержка
        //tcp://itrf.ru/росинфотех/техподдержка
        public static bool Authorization(string UserName, string Password, bool ЗаписатьВЖурнал)
        {
            if (UserName == null)
                return false;
            else if (string.IsNullOrEmpty(UserName.Trim()))
                throw new Exception("Заполните поле имя пользователя");

            try
            {
                var token = Regex.Match(UserName, @"^(?<DOMAIN>(.+?))[\\/](?<USER>(.+?))(@(?<HOST>(.+)))?$", RegexOptions.Compiled | RegexOptions.Singleline);
                Client.Domain = token.Groups["DOMAIN"].Value.ToLower();
                Client.UserName = token.Groups["USER"].Value;
                Client.Password = Password;
                if (!string.IsNullOrEmpty(token.Groups["HOST"].Value))
                {
                    //var host = token.Groups["HOST"].Value.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    //if (!string.IsNullOrEmpty(host.ElementAtOrDefault(1)))
                    //    DefaultPort = host.ElementAtOrDefault(1);

                    Client.Host = token.Groups["HOST"].Value;
                }
                if (string.IsNullOrEmpty(Client.UserName))
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                throw new Exception(@"Не правильно указано имя пользователя, используйте формат 'компания\Пользователь'.");
            }

            //проверить существует ли пользователь
            try
            {
                var configuration = new Configuration.ConfigurationClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration")));
                var user = configuration.Авторизация(Client.UserName, Client.Password, ЗаписатьВЖурнал, Client.Domain);


                //var token = Host.Split(':');
                //var url = token.ElementAtOrDefault(0);
                //var port = token.ElementAtOrDefault(1);
                //if (string.IsNullOrEmpty(port)) 
                //    port = DefaultPort;

                //var client = new FactoryWrapper<IConfigurationChannel>(
                //    new BasicHttpBinding(),
                //    new EndpointAddress(string.Format("http://" + url + ":" + port + GetPath() + @"/{0}/basic", "Configuration")));
                //var user = client.Execute(proxy =>
                //{
                //    return proxy.Авторизация(Client.UserName, Client.Password, false, Client.Domain);
                //});

                Client.UserName = user.Логин;
                Client.User.id_node = user.id_node;
                Client.User.Тип = user.Тип;
                Client.User.Роли = user.Роли;
                Client.User.ГруппаРаздел = user.ГруппаРаздел;
                Client.User.Группа = user.Группа;
                Client.User.Права.НаборПравДоступа = user.Права;
            }
            catch (NullReferenceException)
            {
                throw new Exception("Не верно указано имя пользователя или пароль.");
            }
            catch (EndpointNotFoundException)
            {
                throw new Exception(string.Format("Не удалось установить связь с сервером ({0}). Проверьте подключение к Интернет.", host));
            }
            catch (TimeoutException)
            {
                throw new Exception(string.Format("Не удалось установить связь с сервером ({0}). Проверьте подключение к Интернет.", host));
            }
            Client.IsAuthorization = true;
            return true;
        }
        public static void Shutdown()
        {
            if (IsAuthorization)
            {
                Domain = UserName = Password = "";
                IsAuthorization = false;
            }
            //DisposeObject();
        }

        /// <summary>
        /// Обновляется значение 'ВремяСессии'
        /// </summary>
        public static void ПродлитьСессиюПользователя(TimeSpan ВремяСесси)
        {
            if (!IsAuthorization) return;

            var configuration = new Configuration.ConfigurationClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration")));
            configuration.АвторизацияПродлитьСессиюПользователя(User.id_node, ВремяСесси, Domain);
        }
        #endregion

        #region Список доменов
        public static string[] СписокДоменов()
        {
            var configuration = new Configuration.ConfigurationClient(DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration")));
            return configuration.СписокДоменов();
        }
        #endregion


        private void WindowsLog(Exception exception)
        {
            try
            {
                var source = (RosService.Client.Domain ?? "") + "@" + (RosService.Client.UserName ?? "");
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, "Программа 4.0");
                }
                EventLog.WriteEntry(source, exception.ToString(), System.Diagnostics.EventLogEntryType.Error, 1, 1);
            }
            catch
            {
            }
        }
    }

    public class AddClientEventArgs : EventArgs
    {
        public object id_parent { get; private set; }
        public string Тип { get; private set; }
        public Dictionary<string, object> Значения { get; private set; }


        public AddClientEventArgs()
        {
        }
        public AddClientEventArgs(object parent, string type, Dictionary<string, object> value)
        {
            id_parent = parent;
            Тип = type;
            Значения = value;
        }
    }
    public class AddClientPostEventArgs : AddClientEventArgs
    {
        public object id_node { get; private set; }

        public AddClientPostEventArgs()
        {
        }
        public AddClientPostEventArgs(decimal node, object parent, string type, Dictionary<string, object> value)
            : base(parent, type, value)
        {
            id_node = node;
        }
    }
}
