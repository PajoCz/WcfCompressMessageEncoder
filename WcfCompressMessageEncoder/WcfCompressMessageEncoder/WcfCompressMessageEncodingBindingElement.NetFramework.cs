﻿#if NETFRAMEWORK
using System;
using System.Configuration;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.Xml;

namespace WcfCompressMessageEncoder
{
    public sealed partial class WcfCompressMessageEncodingBindingElement : IPolicyExportExtension
    {
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
        [ConfigurationProperty("compressionFormat")]
        public string CompressionFormat
        {
            get => (string) base["compressionFormat"];
            set => base["compressionFormat"] = value;
        }

        //Called by the WCF to apply the configuration settings (the property above) to the binding element
        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            var binding = (WcfCompressMessageEncodingBindingElement) bindingElement;
            ApplyConfigurationSetBindingCompressionFormat(binding);
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

        private void ApplyConfigurationSetBindingCompressionFormat(WcfCompressMessageEncodingBindingElement binding)
        {
            if (string.IsNullOrEmpty(CompressionFormat))
            {
                binding.CompressionFormat = WcfCompressMessageEncoder.CompressionFormat.None;
            }
            else
            {
                CompressionFormat enumValue;
                if (!Enum.TryParse(CompressionFormat, true, out enumValue))
                {
                    throw new ArgumentOutOfRangeException($"Configuration CompressionFormat unsupported value '{CompressionFormat}'");
                }
                binding.CompressionFormat = enumValue;
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
#endif