using System.ServiceModel;

namespace WcfCompressMessageEncoder.ConsoleTest
{
    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        string Echo(string input);
    }

    public class Service : IService
    {
        public string Echo(string input)
        {
            return input;
        }
    }
}