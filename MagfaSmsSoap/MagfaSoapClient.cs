using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MagfaSms.Soap
{
    /// <summary>
    /// کلاینت سرویس SOAP v2 مگفا.
    /// مستندات: https://messaging.magfa.com/ui/?public/wiki/api/soap_v2
    /// آدرس سرویس: https://sms.magfa.com/api/soap/sms/v2/server
    /// WSDL:       https://sms.magfa.com/api/soap/sms/v2/server?wsdl
    ///
    /// احراز هویت: HTTP Basic Authentication با نام‌کاربری «username/domain».
    /// متدها: balance , send , mid , statuses , messages
    /// </summary>
    public class MagfaSoapClient : IDisposable
    {
        /// <summary>آدرس پایه‌ی سرویس SOAP</summary>
        public const string DefaultEndpoint = "https://sms.magfa.com/api/soap/sms/v2/server";

        /// <summary>حداکثر تعداد گیرنده/شناسه در هر درخواست (محدودیت رسمی مگفا)</summary>
        public const int MaxPerRequest = 100;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly HttpClient _http;
        private bool _disposed;

        public string Username { get; private set; }

        public string Domain { get; private set; }

        /// <summary>شماره‌ی فرستنده‌ی پیش‌فرض (اگر در درخواست send مشخص نشود)</summary>
        public string DefaultSender { get; set; }

        /// <summary>آدرس سرویس SOAP</summary>
        public string Endpoint { get; private set; }

        public TimeSpan Timeout
        {
            get { return _http.Timeout; }
            set { _http.Timeout = value; }
        }

        /// <summary>آخرین پاکت SOAP ارسال‌شده (برای دیباگ)</summary>
        public string LastRequestXml { get; private set; }

        /// <summary>آخرین پاسخ دریافت‌شده از سرور (برای دیباگ)</summary>
        public string LastResponseXml { get; private set; }

        public MagfaSoapClient(string username, string password, string domain, string defaultSender)
            : this(username, password, domain, defaultSender, null)
        {
        }

        /// <summary>
        /// ساخت کلاینت.
        /// </summary>
        /// <param name="endpoint">آدرس سرویس؛ null یعنی آدرس پیش‌فرض مگفا</param>
        public MagfaSoapClient(string username, string password, string domain, string defaultSender, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("نام کاربری نمی‌تواند خالی باشد.", "username");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("رمز عبور نمی‌تواند خالی باشد.", "password");
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("دامنه نمی‌تواند خالی باشد.", "domain");

            Username = username;
            Domain = domain;
            DefaultSender = defaultSender;
            Endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim().TrimEnd('/');

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(60);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(username + "/" + domain + ":" + password)));
        }

        #region متدهای عمومی (Async)

        /// <summary>دریافت مانده اعتبار حساب</summary>
        public Task<BalanceResult> GetBalanceAsync()
        {
            return GetBalanceInternalAsync();
        }

        /// <summary>ارسال پیامک (با دسته‌بندی خودکار برای بیش از ۱۰۰ گیرنده)</summary>
        public Task<SendResult> SendAsync(SendRequest request)
        {
            return SendInternalAsync(request);
        }

        /// <summary>ارسال یک پیام واحد به یک گیرنده</summary>
        public Task<SendResult> SendAsync(string recipient, string message)
        {
            return SendAsync(new[] { recipient }, message);
        }

        /// <summary>ارسال یک پیام واحد به چند گیرنده</summary>
        public Task<SendResult> SendAsync(string[] recipients, string message)
        {
            if (recipients == null) throw new ArgumentNullException("recipients");
            if (string.IsNullOrEmpty(message)) throw new ArgumentException("متن پیامک نمی‌تواند خالی باشد.", "message");

            return SendInternalAsync(new SendRequest
            {
                Messages = new[] { message },
                Recipients = recipients
            });
        }

        /// <summary>دریافت شناسه‌ی یکتای پیامک از روی uid</summary>
        public Task<MidResult> GetMidAsync(long uid)
        {
            return GetMidInternalAsync(uid);
        }

        /// <summary>پیگیری وضعیت نهایی تحویل پیامک‌ها</summary>
        public Task<StatusesResult> GetStatusesAsync(long[] mids)
        {
            return GetStatusesInternalAsync(mids);
        }

        /// <summary>دریافت پیامک‌های ورودی (حداکثر ۱۰۰)</summary>
        /// <param name="count">تعداد؛ پیش‌فرض ۱۰۰</param>
        /// <param name="shortNumber">فقط پیامک‌های ورودی یک شماره‌ی خاص (اختیاری)</param>
        public Task<ReceivedMessagesResult> GetReceivedMessagesAsync(int count = 100, string shortNumber = null)
        {
            return GetReceivedMessagesInternalAsync(count, shortNumber);
        }

        #endregion

        #region متدهای همگام (Sync)

        public BalanceResult GetBalance()
        {
            return GetBalanceAsync().GetAwaiter().GetResult();
        }

        public SendResult Send(SendRequest request)
        {
            return SendAsync(request).GetAwaiter().GetResult();
        }

        public SendResult Send(string recipient, string message)
        {
            return SendAsync(recipient, message).GetAwaiter().GetResult();
        }

        public SendResult Send(string[] recipients, string message)
        {
            return SendAsync(recipients, message).GetAwaiter().GetResult();
        }

        public MidResult GetMid(long uid)
        {
            return GetMidAsync(uid).GetAwaiter().GetResult();
        }

        public StatusesResult GetStatuses(long[] mids)
        {
            return GetStatusesAsync(mids).GetAwaiter().GetResult();
        }

        public ReceivedMessagesResult GetReceivedMessages(int count = 100, string shortNumber = null)
        {
            return GetReceivedMessagesAsync(count, shortNumber).GetAwaiter().GetResult();
        }

        #endregion

        #region پیاده‌سازی داخلی

        private async Task<BalanceResult> GetBalanceInternalAsync()
        {
            XElement payload = await InvokeAsync("balance", null).ConfigureAwait(false);

            return new BalanceResult
            {
                Status = 0,
                Balance = SoapMessage.ChildValue(payload, "balance") == null
                    ? (decimal?)null
                    : SoapMessage.ChildDecimal(payload, "balance")
            };
        }

        private async Task<SendResult> SendInternalAsync(SendRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Recipients == null || request.Recipients.Length == 0)
                throw new ArgumentException("آرایه‌ی recipients نمی‌تواند خالی باشد (کد خطای ۱۰۶).", "request.Recipients");
            if (request.Messages == null || request.Messages.Length == 0)
                throw new ArgumentException("آرایه‌ی messages نمی‌تواند خالی باشد.", "request.Messages");
            if (request.Senders == null || request.Senders.Length == 0)
                throw new ArgumentException("آرایه‌ی senders نمی‌تواند خالی باشد (کد خطای ۱۰۸).", "request.Senders");

            int total = request.Recipients.Length;
            if (request.Messages.Length != total && request.Messages.Length != 1)
                throw new ArgumentException(
                    "طول آرایه‌ی messages باید با recipients برابر باشد یا یک پیام واحد باشد (کد خطای ۱۰۱).",
                    "request.Messages");

            var result = new SendResult { Status = 0 };

            for (int start = 0; start < total; start += MaxPerRequest)
            {
                int count = Math.Min(MaxPerRequest, total - start);

                string[] recipients = Slice(request.Recipients, start, count);
                string[] messages = request.Messages.Length == 1
                    ? Repeat(request.Messages[0], count)
                    : Slice(request.Messages, start, count);
                string[] senders = request.Senders.Length == 1
                    ? Repeat(request.Senders[0], count)
                    : Slice(request.Senders, start, count);

                if (senders.Length != count)
                    throw new ArgumentException(
                        "طول آرایه‌ی senders باید با recipients برابر باشد یا یک مقدار واحد باشد (کد خطای ۱۰۳).",
                        "request.Senders");

                var parameters = new List<XElement>
                {
                    SoapMessage.StringArray("messages", messages),
                    SoapMessage.StringArray("senders", senders),
                    SoapMessage.StringArray("recipients", recipients)
                };

                AddOptionalArray(parameters, "uids", request.Uids, start, count, total);
                AddOptionalArray(parameters, "encodings", request.Encodings, start, count, total);
                AddOptionalArray(parameters, "udhs", request.Udhs, start, count, total);
                AddOptionalArray(parameters, "priorities", request.Priorities, start, count, total);

                XElement payload = await InvokeAsync("send", parameters.ToArray()).ConfigureAwait(false);
                MergeSendResponse(result, payload);
            }

            result.SuccessCount = 0;
            result.FailCount = 0;
            foreach (SendMessageResult message in result.Messages)
            {
                if (message.Status == 0) result.SuccessCount++;
                else result.FailCount++;
            }

            return result;
        }

        private static void AddOptionalArray(List<XElement> parameters, string name, Array values, int start, int count, int total)
        {
            if (values == null || values.Length == 0) return;

            if (values.Length == 1)
            {
                parameters.Add(BuildArray(name, values, 0, count));
            }
            else if (values.Length == total)
            {
                parameters.Add(BuildArray(name, values, start, count));
            }
            else
            {
                throw new ArgumentException(
                    "طول آرایه‌ی " + name + " باید با recipients برابر باشد یا یک مقدار واحد باشد.",
                    name);
            }
        }

        private static XElement BuildArray(string name, Array values, int start, int count)
        {
            var element = new XElement(name);
            for (int i = start; i < start + count; i++)
            {
                element.Add(new XElement("item", Convert.ToString(values.GetValue(i), Inv)));
            }
            return element;
        }

        private async Task<MidResult> GetMidInternalAsync(long uid)
        {
            var parameters = new[] { SoapMessage.ValueElement("uid", uid) };
            XElement payload = await InvokeAsync("mid", parameters).ConfigureAwait(false);

            return new MidResult
            {
                Status = 0,
                Mid = SoapMessage.ChildLong(payload, "mid", -1)
            };
        }

        private async Task<StatusesResult> GetStatusesInternalAsync(long[] mids)
        {
            if (mids == null || mids.Length == 0)
                throw new ArgumentException("آرایه‌ی mids نمی‌تواند خالی باشد.", "mids");

            var result = new StatusesResult { Status = 0 };

            for (int start = 0; start < mids.Length; start += MaxPerRequest)
            {
                int count = Math.Min(MaxPerRequest, mids.Length - start);

                var parameters = new[]
                {
                    SoapMessage.LongArray("mids", Slice(mids, start, count))
                };

                XElement payload = await InvokeAsync("statuses", parameters).ConfigureAwait(false);
                MergeStatusesResponse(result, payload);
            }

            return result;
        }

        private async Task<ReceivedMessagesResult> GetReceivedMessagesInternalAsync(int count, string shortNumber)
        {
            if (count <= 0 || count > MaxPerRequest) count = MaxPerRequest;

            var parameters = new List<XElement>
            {
                SoapMessage.ValueElement("count", count)
            };

            if (!string.IsNullOrWhiteSpace(shortNumber))
            {
                parameters.Add(SoapMessage.ValueElement("shortNumber", shortNumber));
            }

            XElement payload = await InvokeAsync("messages", parameters.ToArray()).ConfigureAwait(false);

            var result = new ReceivedMessagesResult { Status = 0 };

            foreach (XElement item in SoapMessage.ChildElements(payload, "messages"))
            {
                result.Messages.Add(new ReceivedMessage
                {
                    Body = SoapMessage.ChildValue(item, "body"),
                    SenderNumber = SoapMessage.ChildValue(item, "senderNumber"),
                    RecipientNumber = SoapMessage.ChildValue(item, "recipientNumber"),
                    Date = SoapMessage.ChildValue(item, "date")
                });
            }

            return result;
        }

        /// <summary>
        /// ارسال پاکت SOAP و استخراج عنصر نتیجه.
        /// در صورت status نامعتبر، MagfaException پرتاب می‌کند.
        /// </summary>
        private async Task<XElement> InvokeAsync(string operation, XElement[] parameters)
        {
            EnsureNotDisposed();

            string requestXml = SoapMessage.BuildRequest(operation, parameters);
            LastRequestXml = requestXml;

            HttpResponseMessage response;
            using (var content = new StringContent(requestXml, Encoding.UTF8, "text/xml"))
            {
                content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
                response = await _http.PostAsync(Endpoint, content).ConfigureAwait(false);
            }

            using (response)
            {
                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LastResponseXml = text;

                if (!response.IsSuccessStatusCode)
                {
                    throw new MagfaException(
                        (int)response.StatusCode,
                        string.Format(Inv,
                            "خطای HTTP {0} ({1}) از سرور مگفا. پاسخ: {2}",
                            (int)response.StatusCode, response.ReasonPhrase, Truncate(text, 500)));
                }

                XElement body = SoapMessage.FindBody(text);
                if (body == null)
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا پاکت SOAP معتبر نیست: " + Truncate(text, 500));

                string fault = FindFaultText(body);
                if (fault != null)
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "خطای SOAP از سرور مگفا: " + fault);

                XElement payload = SoapMessage.FindPayload(body);
                if (payload == null)
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا خالی است: " + Truncate(text, 500));

                string statusValue = SoapMessage.ChildValue(payload, "status");
                int status;
                if (statusValue == null || !int.TryParse(statusValue, NumberStyles.Integer, Inv, out status))
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا بدون فیلد status است: " + Truncate(text, 500));

                if (status != 0)
                    throw new MagfaException(status);

                return payload;
            }
        }

        private static string FindFaultText(XElement body)
        {
            XElement fault = body.Descendants().FirstOrDefault(
                e => string.Equals(e.Name.LocalName, "Fault", StringComparison.Ordinal));
            if (fault == null) return null;

            XElement faultString = fault.Elements().FirstOrDefault(
                e => string.Equals(e.Name.LocalName, "faultstring", StringComparison.Ordinal));

            return faultString == null ? fault.Value : faultString.Value;
        }

        private static void MergeSendResponse(SendResult result, XElement payload)
        {
            foreach (XElement item in SoapMessage.ChildElements(payload, "messages"))
            {
                result.Messages.Add(new SendMessageResult
                {
                    Status = SoapMessage.ChildInt(item, "status"),
                    Id = SoapMessage.ChildLong(item, "id"),
                    UserId = SoapMessage.ChildLong(item, "userId"),
                    Parts = SoapMessage.ChildInt(item, "parts"),
                    Tariff = SoapMessage.ChildDecimal(item, "tariff"),
                    Alphabet = SoapMessage.ChildValue(item, "alphabet"),
                    Recipient = SoapMessage.ChildValue(item, "recipient")
                });
            }
        }

        private static void MergeStatusesResponse(StatusesResult result, XElement payload)
        {
            foreach (XElement item in SoapMessage.ChildElements(payload, "dlrs"))
            {
                result.Dlrs.Add(new DeliveryStatus
                {
                    Mid = SoapMessage.ChildLong(item, "mid", -1),
                    Status = SoapMessage.ChildInt(item, "status"),
                    Date = SoapMessage.ChildValue(item, "date"),
                    Smsc = SoapMessage.ChildValue(item, "smsc")
                });
            }
        }

        #endregion

        #region ابزارهای کمکی

        private static T[] Slice<T>(T[] source, int start, int count)
        {
            var result = new T[count];
            Array.Copy(source, start, result, 0, count);
            return result;
        }

        private static T[] Repeat<T>(T value, int count)
        {
            var result = new T[count];
            for (int i = 0; i < count; i++) result[i] = value;
            return result;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("MagfaSoapClient");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_http != null) _http.Dispose();
        }

        #endregion
    }
}
