using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;

namespace WcfCompressMessageEncoder.ConsoleTest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            StartService(typeof(Service));

            var bindingWs = new WSHttpBinding(SecurityMode.None);
            CallWcf(bindingWs, "http://pbalas:8002/WcfCompressMessageEncoder.Service");

            var encoding = new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement());
            var transport = new HttpTransportBindingElement();
            var binding = new CustomBinding(encoding, transport);
            CallWcf(binding, "http://pbalas:8002/WcfCompressMessageEncoder.Service/GZipText");

            encoding = new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement());
            binding = new CustomBinding(encoding, transport);
            CallWcf(binding, "http://pbalas:8002/WcfCompressMessageEncoder.Service/GZipBinary");

            Console.WriteLine("PRESS ANY KEY TO EXIT ...");
            Console.ReadKey();
        }

        private static void StartService(Type serviceType)
        {
            var service = new ServiceHost(serviceType);
            service.Open();
            Console.WriteLine($"Service opened at baseAddress {service.BaseAddresses.First()}");
        }

        private static void CallWcf(Binding binding, string address)
        {
            Console.WriteLine($"Address : {address}");
            var channelWs = ChannelFactory<IService>.CreateChannel(binding, new EndpointAddress(address));
            try
            {
                var sb = new StringBuilder();
                for (var i = 0; i < 1000; i++)
                    sb.Append("Input text is here.");
                var text = sb.ToString();
                Console.WriteLine($"Send    : {text}");
                var result = channelWs.Echo(text);
                Console.WriteLine($"Received: {result}");
                if (result != text)
                    throw new Exception("Returns another text than input");
            }
            finally
            {
                ((IClientChannel) channelWs).Close();
            }
        }
    }
}