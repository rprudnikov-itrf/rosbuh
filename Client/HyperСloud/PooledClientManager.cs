using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;
using RosService.ServiceModel;
using System.Threading;

namespace HyperСloud
{
    internal class PooledClientManager<T> : IDisposable where T : class
    {
        private readonly object lockobj = new object();
        private static Dictionary<string, EndpointAddress> Endpoints = new Dictionary<string, EndpointAddress>();
        private List<Connection<T>> Connections = new List<Connection<T>>();
        private readonly int PoolSize = 5;
        private int CurrentClient;
        private string Host;
        private string Contract;

        public PooledClientManager(int poolSize, string host, string contract)
        {
            PoolSize = poolSize;
            Host = host;
            Contract = contract;

            var endpoint = GetEndpointAddress();
            var binding = GetBinding();
            for (int i = 0; i < poolSize; i++)
                Connections.Add(new Connection<T>(binding, endpoint));
        }

        #region default bindings
        private static System.ServiceModel.Channels.Binding __BindingDefault;
        public static System.ServiceModel.Channels.Binding BindingDefault
        {
            get
            {
                if (__BindingDefault == null)
                {
                    var _CustomBinding = new CustomBinding()
                    {
                        SendTimeout = TimeSpan.FromSeconds(60),
                        ReceiveTimeout = TimeSpan.FromSeconds(60)
                    };
                    var _BinaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement()
                    {
                        MaxReadPoolSize = 65536,
                        MaxWritePoolSize = 65536
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
                        MaxReceivedMessageSize = int.MaxValue,
                        MaxBufferSize = int.MaxValue,
                    });
                    __BindingDefault = _CustomBinding;
                }

                return __BindingDefault;
            }
        }


        private static System.ServiceModel.Channels.Binding __BindingFiles;
        public static System.ServiceModel.Channels.Binding BindingFiles
        {
            get
            {
                if (__BindingFiles == null)
                {
                    var binding = new CustomBinding()
                    {
                        SendTimeout = TimeSpan.FromMinutes(5),
                        ReceiveTimeout = TimeSpan.FromMinutes(5)
                    };
                    var _BinaryMessageEncodingBindingElement2 = new BinaryMessageEncodingBindingElement();
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxStringContentLength = int.MaxValue;
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxArrayLength = int.MaxValue;
                    _BinaryMessageEncodingBindingElement2.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
                    binding.Elements.Add(_BinaryMessageEncodingBindingElement2);
                    binding.Elements.Add(new HttpTransportBindingElement()
                    {
                        MaxBufferSize = int.MaxValue,
                        MaxReceivedMessageSize = int.MaxValue,
                        TransferMode = TransferMode.Streamed
                    });

                    __BindingFiles = binding;
                }

                return __BindingFiles;
            }
        }
        #endregion

        public EndpointAddress GetEndpointAddress()
        {
            //по умолчанию порт для всех удаленных серверов
            if (System.Configuration.ConfigurationManager.AppSettings["RosService.Host"] != null)
            {
                //проверить хост по-умолчанию в web.config
                Host = System.Configuration.ConfigurationManager.AppSettings["RosService.Host"];
            }

            if (!Endpoints.ContainsKey(Host))
            {
                if (typeof(T) == typeof(RosService.Files.IFile))
                {
                    Endpoints.Add(Host, new EndpointAddress(string.Format("http://{0}:8080/RosService/{1}/binary", !string.IsNullOrEmpty(Host) ? Host : @"89.108.72.236", Contract)));
                }
                else
                {
                    Endpoints.Add(Host, new EndpointAddress(string.Format("http://{0}:8080/RosService/{1}/gzip", !string.IsNullOrEmpty(Host) ? Host : @"89.108.72.236", Contract)));
                }
            }

            return Endpoints[Host];
        }
        public System.ServiceModel.Channels.Binding GetBinding()
        {
            if (typeof(T) == typeof(RosService.Files.IFile))
            {
                return BindingFiles;
            }
            return BindingDefault;
        }

        public Connection<T> GetClient()
        {
            var current = null as Connection<T>;
            lock (lockobj)
            {
                while (current == null)
                {
                    if (CurrentClient >= PoolSize)
                        CurrentClient = 0;

                    current = Connections[CurrentClient];
                    if (current.IsFaulted)
                    {
                        current.Dispose();
                        current = new Connection<T>(GetBinding(), GetEndpointAddress());
                        Connections[CurrentClient] = current;
                    }
                    Interlocked.Increment(ref CurrentClient);
                }
            }            
            return current;
        }

        public void Dispose()
        {
            foreach (var item in Connections)
                item.Dispose();
        }
    }

    internal class Connection<T> : ClientBase<T>, IDisposable where T : class
    {
        public Connection(System.ServiceModel.Channels.Binding binding, EndpointAddress endpoint)
            : base(binding, endpoint)
        {
       
        }

        public new T Channel
        {
            get
            {
                return base.Channel;
            }
        }

        public void Dispose()
        {
            try
            {
                if (State == CommunicationState.Faulted)
                    Abort();
                else if (State != CommunicationState.Closed)
                    Close();
            }
            catch (CommunicationException)
            {
                Abort();
            }
            catch (TimeoutException)
            {
                Abort();
            }
            catch (Exception)
            {
            }

            //IsInitialize = false;
        }

        public bool IsFaulted
        {
            get
            {
                switch (State)
                {
                    case CommunicationState.Faulted:
                    case CommunicationState.Closed:
                    case CommunicationState.Closing:
                        return true;
                }
                return false;
            }
        }
    }
}
