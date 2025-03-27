using System;
using System.ServiceModel.Channels;
using System.Xml;

namespace WcfCompressMessageEncoder
{
    public enum CompressionFormat
    {
        None,
        GZip,
        Deflate,
        Brotli
    }
    
    // This is constants for WcfCompress message encoding policy.
    internal static class WcfCompressMessageEncodingPolicyConstants
    {
        public const string WcfCompressEncodingName = "WcfCompressEncoding";
        public const string WcfCompressEncodingNamespace = "http://schemas.microsoft.com/ws/06/2004/mspolicy/netWcfCompress1";
        public const string WcfCompressEncodingPrefix = "WcfCompress";
    }

    //This is the binding element that, when plugged into a custom binding, will enable the WcfCompress encoder
    public sealed partial class WcfCompressMessageEncodingBindingElement
        : MessageEncodingBindingElement //BindingElement
    {
        //We will use an inner binding element to store information required for the inner encoder

        //By default, use the default text encoder as the inner encoder
        public WcfCompressMessageEncodingBindingElement()
            : this(new TextMessageEncodingBindingElement(), CompressionFormat.None)
        {
        }

        public WcfCompressMessageEncodingBindingElement(MessageEncodingBindingElement messageEncoderBindingElement, CompressionFormat compressionFormat)
        {
            InnerMessageEncodingBindingElement = messageEncoderBindingElement;
            CompressionFormat = compressionFormat;
        }

        public MessageEncodingBindingElement InnerMessageEncodingBindingElement { get; set; }

        public override MessageVersion MessageVersion
        {
            get => InnerMessageEncodingBindingElement.MessageVersion;
            set => InnerMessageEncodingBindingElement.MessageVersion = value;
        }

        //Main entry point into the encoder binding element. Called by WCF to get the factory that will create the
        //message encoder
        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new WcfCompressMessageEncoderFactory(InnerMessageEncodingBindingElement.CreateMessageEncoderFactory(), CompressionFormat);
        }

        public override BindingElement Clone()
        {
            return new WcfCompressMessageEncodingBindingElement(InnerMessageEncodingBindingElement, CompressionFormat);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
                return InnerMessageEncodingBindingElement.GetProperty<T>(context);
            return base.GetProperty<T>(context);
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            context.BindingParameters.Add(this);
            return context.BuildInnerChannelFactory<TChannel>();
        }

        public CompressionFormat CompressionFormat { get; set; }
    }
}