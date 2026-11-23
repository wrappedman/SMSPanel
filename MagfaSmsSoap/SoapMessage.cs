using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace MagfaSms.Soap
{
    /// <summary>
    /// ساخت پاکت SOAP 1.1 (RPC/Literal) و خواندن پاسخ سرویس مگفا.
    ///
    /// ساختار درخواست برای متد balance به این شکل است:
    ///   &lt;soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"&gt;
    ///     &lt;soap:Body&gt;
    ///       &lt;balance xmlns="webservice.magfa.com" /&gt;
    ///     &lt;/soap:Body&gt;
    ///   &lt;/soap:Envelope&gt;
    ///
    /// منبع ساختار: WSDL سرویس https://sms.magfa.com/api/soap/sms/v2/server?wsdl
    ///   targetNamespace پورت‌تایپ = webservice.magfa.com و سبک سرویس RPC است.
    /// </summary>
    internal static class SoapMessage
    {
        /// <summary>namespace پاکت SOAP</summary>
        public const string EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

        /// <summary>namespace عملیات‌ها در سرویس مگفا</summary>
        public const string OperationNamespace = "webservice.magfa.com";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>ساخت پاکت SOAP برای یک متد</summary>
        /// <param name="operation">نام متد (balance , send , mid , statuses , messages)</param>
        /// <param name="parameters">پارامترهای متد (با نام partName مستندات)</param>
        public static string BuildRequest(string operation, params XElement[] parameters)
        {
            XNamespace soap = EnvelopeNamespace;
            XNamespace op = OperationNamespace;

            var body = new XElement(op + operation);
            if (parameters != null)
            {
                foreach (XElement parameter in parameters)
                {
                    if (parameter != null) body.Add(parameter);
                }
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(soap + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soap", EnvelopeNamespace),
                    new XElement(soap + "Body", body)));

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>پارامتر آرایه‌ای رشته (stringArray)</summary>
        public static XElement StringArray(string name, string[] values)
        {
            var element = new XElement(name);
            if (values == null) return element;

            foreach (string value in values)
            {
                element.Add(new XElement("item", value ?? string.Empty));
            }
            return element;
        }

        /// <summary>پارامتر آرایه‌ای عدد بلند (longArray)</summary>
        public static XElement LongArray(string name, long[] values)
        {
            var element = new XElement(name);
            if (values == null) return element;

            foreach (long value in values)
            {
                element.Add(new XElement("item", value.ToString(Inv)));
            }
            return element;
        }

        /// <summary>پارامتر آرایه‌ای عدد (intArray)</summary>
        public static XElement IntArray(string name, int[] values)
        {
            var element = new XElement(name);
            if (values == null) return element;

            foreach (int value in values)
            {
                element.Add(new XElement("item", value.ToString(Inv)));
            }
            return element;
        }

        /// <summary>پارامتر عددی تکی</summary>
        public static XElement ValueElement(string name, long value)
        {
            return new XElement(name, value.ToString(Inv));
        }

        /// <summary>پارامتر رشته‌ای تکی</summary>
        public static XElement ValueElement(string name, string value)
        {
            return new XElement(name, value ?? string.Empty);
        }

        /// <summary>
        /// پیدا کردن عنصر بدنه‌ی پاسخ؛ هم با namespace استاندارد SOAP 1.1 و هم بدون آن.
        /// </summary>
        public static XElement FindBody(string xml)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception) { return null; }

            XNamespace soap = EnvelopeNamespace;

            XElement body = doc.Descendants(soap + "Body").FirstOrDefault();
            if (body == null)
                body = doc.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Body", StringComparison.Ordinal));

            return body;
        }

        /// <summary>
        /// عنصر اصلی نتیجه را از پاسخ استخراج می‌کند.
        /// در RPC/Literal معمولاً &lt;متدResponse&gt;&lt;return&gt;...&lt;/return&gt;&lt;/متدResponse&gt; است.
        /// </summary>
        public static XElement FindPayload(XElement body)
        {
            if (body == null) return null;

            XElement wrapper = body.Elements().FirstOrDefault();
            if (wrapper == null) return null;

            XElement returned = wrapper.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "return", StringComparison.Ordinal));
            return returned ?? wrapper;
        }

        /// <summary>اولین فرزند مستقیم با این نام (بدون توجه به namespace)</summary>
        public static XElement Child(XElement parent, string localName)
        {
            if (parent == null) return null;

            return parent.Elements().FirstOrDefault(
                e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));
        }

        /// <summary>مقدار متنی اولین فرزند مستقیم با این نام</summary>
        public static string ChildValue(XElement parent, string localName)
        {
            XElement element = Child(parent, localName);
            return element == null ? null : element.Value;
        }

        public static int ChildInt(XElement parent, string localName, int defaultValue = 0)
        {
            string value = ChildValue(parent, localName);
            int result;
            return (value != null && int.TryParse(value, NumberStyles.Integer, Inv, out result))
                ? result
                : defaultValue;
        }

        public static long ChildLong(XElement parent, string localName, long defaultValue = 0)
        {
            string value = ChildValue(parent, localName);
            long result;
            return (value != null && long.TryParse(value, NumberStyles.Integer, Inv, out result))
                ? result
                : defaultValue;
        }

        public static decimal ChildDecimal(XElement parent, string localName, decimal defaultValue = 0)
        {
            string value = ChildValue(parent, localName);
            decimal result;
            return (value != null && decimal.TryParse(value, NumberStyles.Number, Inv, out result))
                ? result
                : defaultValue;
        }

        /// <summary>فرزندان عنصر قاب‌گذار (مثل dlrs یا messages) را برمی‌گرداند</summary>
        public static List<XElement> ChildElements(XElement parent, string containerName)
        {
            var list = new List<XElement>();

            XElement container = Child(parent, containerName);
            if (container == null) return list;

            foreach (XElement item in container.Elements())
            {
                list.Add(item);
            }
            return list;
        }
    }
}
