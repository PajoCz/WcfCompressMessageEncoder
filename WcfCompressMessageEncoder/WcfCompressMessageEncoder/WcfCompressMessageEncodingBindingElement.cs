using System;
using System.Configuration;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.Xml;

namespace WcfCompressMessageEncoder
{
    // This is constants for WcfCompress message encoding policy.
    internal static class WcfCompressMessageEncodingPolicyConstants
    {
        public const string WcfCompressEncodingName = "WcfCompressEncoding";
        public const string WcfCompressEncodingNamespace = "http://schemas.microsoft.com/ws/06/2004/mspolicy/netWcfCompress1";
        public const string WcfCompressEncodingPrefix = "WcfCompress";
    }

    //This is the binding element that, when plugged into a custom binding, will enable the WcfCompress encoder
    public sealed class WcfCompressMessageEncodingBindingElement
        : MessageEncodingBindingElement //BindingElement
            , IPolicyExportExtension
    {
        //We will use an inner binding element to store information required for the inner encoder

        //By default, use the default text encoder as the inner encoder
        public WcfCompressMessageEncodingBindingElement()
            : this(new TextMessageEncodingBindingElement())
        {
        }

        public WcfCompressMessageEncodingBindingElement(MessageEncodingBindingElement messageEncoderBindingElement)
        {
            InnerMessageEncodingBindingElement = messageEncoderBindingElement;
        }

        public MessageEncodingBindingElement InnerMessageEncodingBindingElement { get; set; }

        public override MessageVersion MessageVersion
        {
            get => InnerMessageEncodingBindingElement.MessageVersion;
            set => InnerMessageEncodingBindingElement.MessageVersion = value;
        }

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext policyContext)
        {
            if (policyContext == null)
                throw new ArgumentNullException("policyContext");
            var document = new XmlDocument();
            policyContext.GetBindingAssertions().Add(document.CreateElement(
                WcfCompressMessageEncodingPolicyConstants.WcfCompressEncodingPrefix,
                WcfCompressMessageEncodingPolicyConstants.WcfCompressEncodingName,
                WcfCompressMessageEncodingPolicyConstants.WcfCompressEncodingNamespace));
        }

        //Main entry point into the encoder binding element. Called by WCF to get the factory that will create the
        //message encoder
        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new WcfCompressMessageEncoderFactory(InnerMessageEncodingBindingElement.CreateMessageEncoderFactory());
        }

        public override BindingElement Clone()
        {
            return new WcfCompressMessageEncodingBindingElement(InnerMessageEncodingBindingElement);
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

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            context.BindingParameters.Add(this);
            return context.BuildInnerChannelListener<TChannel>();
        }

        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            context.BindingParameters.Add(this);
            return context.CanBuildInnerChannelListener<TChannel>();
        }
    }

    //This class is necessary to be able to plug in the WcfCompress encoder binding element through
    //a configuration file
    public class WcfCompressMessageEncodingElement : BindingElementExtensionElement
    {
        //Called by the WCF to discover the type of binding element this config section enables
        public override Type BindingElementType => typeof(WcfCompressMessageEncodingBindingElement);

        //The only property we need to configure for our binding element is the type of
        //inner encoder to use. Here, we support text and binary.
        [ConfigurationProperty("innerMessageEncoding", DefaultValue = "textMessageEncoding")]
        public string InnerMessageEncoding
        {
            get => (string) base["innerMessageEncoding"];
            set => base["innerMessageEncoding"] = value;
        }

        //The only property we need to configure for our binding element is the type of
        //inner encoder to use. Here, we support text and binary.
        [ConfigurationProperty("compressionFormat", DefaultValue = "GZip")]
        public string CompressionFormat
        {
            get => (string) base["compressionFormat"];
            set => base["compressionFormat"] = value;
        }

        //Called by the WCF to apply the configuration settings (the property above) to the binding element
        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            var binding = (WcfCompressMessageEncodingBindingElement) bindingElement;
            var propertyInfo = ElementInformation.Properties;
            if (propertyInfo["innerMessageEncoding"].ValueOrigin != PropertyValueOrigin.Default)
                switch (InnerMessageEncoding)
                {
                    case "textMessageEncoding":
                        binding.InnerMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
                        break;
                    case "binaryMessageEncoding":
                        binding.InnerMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
                        break;
                }
        }

        //Called by the WCF to create the binding element
        protected override BindingElement CreateBindingElement()
        {
            var bindingElement = new WcfCompressMessageEncodingBindingElement();
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }
    }
}