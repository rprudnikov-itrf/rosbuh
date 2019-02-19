using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

//http://www.rsdn.ru/article/dotnet/WCF_MultithreadClient.xml

namespace RosService
{
    public class FactoryWrapper<TChannel> where TChannel : class
    {
        private static ChannelFactory<TChannel> _factory;

        // или любой другой набор параметров, который вам нужен
        // для корректного создания фабрики...
        public FactoryWrapper(Binding binding, EndpointAddress endpointAddress)
        {
            _factory = new ChannelFactory<TChannel>(binding, endpointAddress);
        }

        public TResult Execute<TResult>(Func<TChannel, TResult> action)
        {
            var proxy = default(TChannel);
            TResult result;
            try
            {
                proxy = _factory.CreateChannel();
                //((IClientChannel)proxy).Open();
                result = action(proxy);
                ((IClientChannel)proxy).Close();
            }
            catch (Exception)
            {
                if (proxy != null)
                    ((IClientChannel)proxy).Abort();
                throw;
            }
            return result;
        }

        //Использовать же подобную обертку довольно просто, например, так:
        //var factoryWrapper = new FactoryWrapper<ITestService>(binding, endpointAddress);
        //// ...
        //var result = factoryWrapper.Execute(proxy => proxy.SomeMethodCall(...));
        //В случае необходимости обращение к нескольким методам вполне можно объединить в сессию:

        //var result = factoryWrapper.Execute(proxy => 
        //{ 
        //    proxy.SessionMethod1();
        //    return proxy.SessionMethod2();
        //});
    }
}
