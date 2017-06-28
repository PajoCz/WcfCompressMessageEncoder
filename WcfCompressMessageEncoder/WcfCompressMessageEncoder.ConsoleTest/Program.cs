using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            Console.WriteLine("Start Fiddler and watch transfered data (size of Http request/response)");

            //not localhost but real hostName of running pc - must be for capturing by Fiddler
            var hostName = System.Environment.MachineName;

            Dictionary<string, Binding> addressBinding = new Dictionary<string, Binding>()
            {
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/", new WSHttpBinding(SecurityMode.None)},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/Text", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(), String.Empty), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/Binary", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(), String.Empty), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/GZipText", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(), "GZip"), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/GZipBinary", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(), "GZip"), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/DeflateText", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(), "Deflate"), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/DeflateBinary", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(), "Deflate"), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/BrotliText", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(), "Brotli"), new HttpTransportBindingElement())},
                {$"http://{hostName}:8002/WcfCompressMessageEncoder.Service/BrotliBinary", new CustomBinding(new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(), "Brotli"), new HttpTransportBindingElement())},
            };

            for (int i = 0; i < 10; i++)
            {
                foreach (var kvp in addressBinding)
                {
                    CallWcf(kvp.Value, kvp.Key);
                }
            }

            Console.WriteLine(Environment.NewLine + "Performance summary:");
            foreach (var kvp in CallElapsed)
            {
                Console.WriteLine($"{kvp.Key} called {kvp.Value.Item2}x with time {kvp.Value.Item1}. Average call was {kvp.Value.Item1.TotalMilliseconds/ kvp.Value.Item2} ms");
            }

            Console.WriteLine(Environment.NewLine + "PRESS ANY KEY TO EXIT ...");
            Console.ReadKey();
        }

        private static void StartService(Type serviceType)
        {
            var service = new ServiceHost(serviceType);
            service.Open();
            Console.WriteLine($"Service opened at baseAddress {service.BaseAddresses.First()}");
        }

        private static Dictionary<string, Tuple<TimeSpan, int>> CallElapsed = new Dictionary<string, Tuple<TimeSpan, int>>();

        private static void CallWcf(Binding binding, string address)
        {
            var channelWs = ChannelFactory<IService>.CreateChannel(binding, new EndpointAddress(address));
            try
            {
                var sb = new StringBuilder();
                for (var i = 0; i < 1000; i++)
                    sb.Append("Input text is here.");
                var text = sb.ToString();
                Stopwatch sw = Stopwatch.StartNew();
                var result = channelWs.Echo(text);
                sw.Stop();
                if (result != text)
                    throw new Exception("Returns another text than input");
                var addressPostfix = address.Remove(0, address.LastIndexOf("/"));
                Console.WriteLine($"Call {addressPostfix} elapsed {sw.Elapsed}");

                Tuple<TimeSpan, int> elapsedItem;
                if (CallElapsed.TryGetValue(addressPostfix, out elapsedItem))
                {
                    CallElapsed[addressPostfix] = new Tuple<TimeSpan, int>(elapsedItem.Item1 + sw.Elapsed, elapsedItem.Item2+1);
                }
                else
                {
                    CallElapsed.Add(addressPostfix, new Tuple<TimeSpan, int>(sw.Elapsed, 1));
                }
            }
            finally
            {
                ((IClientChannel) channelWs).Close();
            }
        }
    }
}