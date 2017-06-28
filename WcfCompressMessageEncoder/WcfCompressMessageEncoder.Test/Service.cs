using System.ServiceModel;

namespace WcfCompressMessageEncoder.Test
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