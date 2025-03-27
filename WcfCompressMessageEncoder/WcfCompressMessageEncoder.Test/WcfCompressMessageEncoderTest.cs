using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using NUnit.Framework;

namespace WcfCompressMessageEncoder.Test
{
    [TestFixture]
    public class WcfCompressMessageEncoderTest
    {
        [Test]
        public void DetectMismatchBindingClientServer()
        {
            var service = StartService(typeof(Service));
            try
            {
                var binding =
                    new CustomBinding(
                        new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(), CompressionFormat.GZip),
                        new HttpTransportBindingElement());
                var address = $"http://{Environment.MachineName}:8002/WcfCompressMessageEncoder.Service/DeflateText";
                Assert.Throws<ProtocolException>(() => CallWcf(address, binding));
            }
            finally
            {
                service.Close();
            }
        }


        [Test]
        public void PerformanceTestAllBindings()
        {
            var service = StartService(typeof(Service));
            try
            {
                Console.WriteLine("Start Fiddler and watch transfered data (size of Http request/response)");

                //not localhost but real hostName of running pc - must be for capturing by Fiddler
                var hostName = System.Environment.MachineName;

                Dictionary<string, Binding> addressBinding = new Dictionary<string, Binding>()
                {
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/",
                        new WSHttpBinding(SecurityMode.None)
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/Text",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(),
                                CompressionFormat.None), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/Binary",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(),
                                CompressionFormat.None), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/GZipText",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(),
                                CompressionFormat.GZip), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/GZipBinary",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(),
                                CompressionFormat.GZip), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/DeflateText",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(),
                                CompressionFormat.Deflate), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/DeflateBinary",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(),
                                CompressionFormat.Deflate), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/BrotliText",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new TextMessageEncodingBindingElement(),
                                CompressionFormat.Brotli), new HttpTransportBindingElement())
                    },
                    {
                        $"http://{hostName}:8002/WcfCompressMessageEncoder.Service/BrotliBinary",
                        new CustomBinding(
                            new WcfCompressMessageEncodingBindingElement(new BinaryMessageEncodingBindingElement(),
                                CompressionFormat.Brotli), new HttpTransportBindingElement())
                    },
                };

                Dictionary<string, Tuple<TimeSpan, int>> callElapsed = new Dictionary<string, Tuple<TimeSpan, int>>();
                for (int i = 0; i < 10; i++)
                {
                    foreach (var kvp in addressBinding)
                    {
                        CallWcf(kvp.Key, kvp.Value, callElapsed);
                    }
                }

                Console.WriteLine(Environment.NewLine + "Performance summary:");
                foreach (var kvp in callElapsed)
                {
                    Console.WriteLine(
                        $"{kvp.Key} called {kvp.Value.Item2}x with time {kvp.Value.Item1}. Average call was {kvp.Value.Item1.TotalMilliseconds / kvp.Value.Item2} ms");
                }
            }
            finally
            {
                service.Close();
            }
        }

        private ServiceHost StartService(Type serviceType)
        {
            var service = new ServiceHost(serviceType);
            service.Open();
            Console.WriteLine($"Service opened at baseAddress {service.BaseAddresses.First()}");
            return service;
        }


        private void CallWcf(string address, Binding binding, Dictionary<string, Tuple<TimeSpan, int>> callElapsed = null)
        {
            var channelWs = new ChannelFactory<IService>(binding, new EndpointAddress(address)).CreateChannel();
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

                if (callElapsed != null)
                {
                    Tuple<TimeSpan, int> elapsedItem;
                    if (callElapsed.TryGetValue(addressPostfix, out elapsedItem))
                    {
                        callElapsed[addressPostfix] =
                            new Tuple<TimeSpan, int>(elapsedItem.Item1 + sw.Elapsed, elapsedItem.Item2 + 1);
                    }
                    else
                    {
                        callElapsed.Add(addressPostfix, new Tuple<TimeSpan, int>(sw.Elapsed, 1));
                    }
                }
            }
            finally
            {
                ((IClientChannel)channelWs).Close();
            }
        }

    }
}
