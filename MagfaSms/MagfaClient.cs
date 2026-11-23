using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MagfaSms
{
    /// <summary>
    /// کلاینت سرویس HTTP v2 مگفا.
    /// مستندات: https://messaging.magfa.com/ui/?public/wiki/api/http_v2
    ///
    /// احراز هویت به‌صورت HTTP Basic Auth با نام‌کاربری «username/domain» انجام می‌شود.
    /// متدها: balance , send , mid , statuses , messages
    /// </summary>
    public class MagfaClient : IDisposable
    {
        /// <summary>آدرس پایه‌ی سرویس</summary>
        public const string DefaultBaseUrl = "https://sms.magfa.com/api/http/sms/v2";

        /// <summary>حداکثر تعداد گیرنده/شناسه در هر درخواست (محدودیت رسمی مگفا)</summary>
        public const int MaxPerRequest = 100;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly HttpClient _http;
        private readonly JavaScriptSerializer _json;
        private bool _disposed;

        public string Username { get; private set; }

        public string Domain { get; private set; }

        /// <summary>شماره‌ی فرستنده‌ی پیش‌فرض (در صورتی که در درخواست send مشخص نشود)</summary>
        public string DefaultSender { get; set; }

        public string BaseUrl { get; private set; }

        public TimeSpan Timeout
        {
            get { return _http.Timeout; }
            set { _http.Timeout = value; }
        }

        /// <summary>
        /// ساخت کلاینت.
        /// </summary>
        /// <param name="username">نام‌کاربری پنل مگفا</param>
        /// <param name="password">رمز عبور سرویس (در پنل زیر «مدیریت حساب » رمز و امنیت ساخته می‌شود)</param>
        /// <param name="domain">دامنه‌ی حساب مگفا</param>
        /// <param name="defaultSender">شماره‌ی فرستنده‌ی پیش‌فرض (مثلاً 3000xxxxx)</param>
        public MagfaClient(string username, string password, string domain, string defaultSender)
            : this(username, password, domain, defaultSender, null)
        {
        }

        /// <summary>
        /// ساخت کلاینت با امکان تعویض آدرس پایه (مثلاً برای تست یا پروکسی).
        /// </summary>
        /// <param name="baseUrl">آدرس پایه‌ی سرویس؛ null یعنی آدرس پیش‌فرض مگفا</param>
        public MagfaClient(string username, string password, string domain, string defaultSender, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("نام کاربری نمی‌تواند خالی باشد.", "username");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("رمز عبور نمی‌تواند خالی باشد.", "password");
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("دامنه نمی‌تواند خالی باشد.", "domain");

            Username = username;
            Domain = domain;
            DefaultSender = defaultSender;
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');

            // اطمینان از TLS 1.2 روی ویندوز‌های قدیمی‌تر
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                // پاسخ‌های GZIP شده‌ی مگفا به‌صورت خودکار بازگشایی می‌شوند
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(60);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP Basic Authentication با نام‌کاربری «username/domain»
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(username + "/" + domain + ":" + password)));

            _json = new JavaScriptSerializer();
            _json.MaxJsonLength = 10 * 1024 * 1024;
        }

        #region متدهای عمومی (Async)

        /// <summary>دریافت مانده اعتبار حساب (ریال)</summary>
        public Task<BalanceResult> GetBalanceAsync()
        {
            return GetBalanceInternalAsync();
        }

        /// <summary>
        /// ارسال پیامک. اگر تعداد گیرندگان بیش از ۱۰۰ باشد، به‌صورت خودکار دسته‌بندی می‌شود.
        /// </summary>
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
                Recipients = recipients,
                Messages = new[] { message }
            });
        }

        /// <summary>دریافت شناسه‌ی یکتای پیامک (mid) از روی شناسه‌ی کاربر (uid)</summary>
        public Task<MidResult> GetMidAsync(long uid)
        {
            return GetMidInternalAsync(uid);
        }

        /// <summary>پیگیری وضعیت نهایی تحویل پیامک‌ها از روی شناسه‌ی یکتای آن‌ها</summary>
        public Task<StatusesResult> GetStatusesAsync(long[] mids)
        {
            return GetStatusesInternalAsync(mids);
        }

        /// <summary>دریافت پیامک‌های ورودی (حداکثر ۱۰۰ عدد)</summary>
        public Task<ReceivedMessagesResult> GetReceivedMessagesAsync(int count = 100)
        {
            return GetReceivedMessagesInternalAsync(count);
        }

        #endregion

        #region متدهای همگام (Sync) — برای برنامه‌های دسکتاپ/فرم‌محور

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

        public ReceivedMessagesResult GetReceivedMessages(int count = 100)
        {
            return GetReceivedMessagesAsync(count).GetAwaiter().GetResult();
        }

        #endregion

        #region پیاده‌سازی داخلی

        private async Task<BalanceResult> GetBalanceInternalAsync()
        {
            IDictionary data = await InvokeAsync("balance", false, null).ConfigureAwait(false);
            return new BalanceResult
            {
                Status = 0,
                Balance = GetNullableLong(data, "balance")
            };
        }

        private async Task<SendResult> SendInternalAsync(SendRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Recipients == null || request.Recipients.Length == 0)
                throw new ArgumentException("آرایه‌ی recipients نمی‌تواند خالی باشد (کد خطای ۱۰۶).", "request.Recipients");
            if (request.Messages == null || request.Messages.Length == 0)
                throw new ArgumentException("آرایه‌ی messages نمی‌تواند خالی باشد.", "request.Messages");

            int total = request.Recipients.Length;
            if (request.Messages.Length != total && request.Messages.Length != 1)
                throw new ArgumentException(
                    "طول آرایه‌ی messages باید با recipients برابر باشد یا یک پیام واحد باشد (کد خطای ۱۰۱).",
                    "request.Messages");

            var result = new SendResult { Status = 0 };

            // دسته‌بندی به اندازه‌ی ۱۰۰ برای ارسال به تعداد بیش از ۱۰۰ گیرنده
            for (int start = 0; start < total; start += MaxPerRequest)
            {
                int count = Math.Min(MaxPerRequest, total - start);
                Dictionary<string, object> payload = BuildSendPayload(request, start, count);
                IDictionary data = await InvokeAsync("send", true, payload).ConfigureAwait(false);
                MergeSendResponse(result, data);
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

        private async Task<MidResult> GetMidInternalAsync(long uid)
        {
            string method = "mid/" + uid.ToString(Inv);
            IDictionary data = await InvokeAsync(method, false, null).ConfigureAwait(false);
            return new MidResult
            {
                Status = 0,
                Mid = GetLong(data, "mid", -1)
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
                long[] chunk = Slice(mids, start, count);
                string joined = string.Join(",", chunk.Select(x => x.ToString(Inv)).ToArray());
                IDictionary data = await InvokeAsync("statuses/" + joined, false, null).ConfigureAwait(false);
                MergeStatusesResponse(result, data);
            }

            return result;
        }

        private async Task<ReceivedMessagesResult> GetReceivedMessagesInternalAsync(int count)
        {
            if (count <= 0 || count > MaxPerRequest) count = MaxPerRequest;

            string method = "messages/" + count.ToString(Inv);
            IDictionary data = await InvokeAsync(method, false, null).ConfigureAwait(false);

            var result = new ReceivedMessagesResult { Status = 0 };

            object messagesObject;
            if (TryGetValue(data, "messages", out messagesObject) && !(messagesObject is string))
            {
                IEnumerable list = messagesObject as IEnumerable;
                if (list != null)
                {
                    foreach (object item in list)
                    {
                        IDictionary dict = item as IDictionary;
                        if (dict == null) continue;

                        result.Messages.Add(new ReceivedMessage
                        {
                            Body = GetString(dict, "body"),
                            SenderNumber = GetString(dict, "senderNumber"),
                            RecipientNumber = GetString(dict, "recipientNumber"),
                            Date = GetString(dict, "date")
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// فراخوانی متد سرویس و بازگرداندن شیء JSON.
        /// در صورت status نامعتبر (غیر از صفر) یک MagfaException پرتاب می‌کند.
        /// </summary>
        private async Task<IDictionary> InvokeAsync(string method, bool post, object payload)
        {
            EnsureNotDisposed();

            string url = BaseUrl + "/" + method;
            HttpResponseMessage response;

            if (post)
            {
                string body = payload == null ? "{}" : _json.Serialize(payload);
                using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
                {
                    response = await _http.PostAsync(url, content).ConfigureAwait(false);
                }
            }
            else
            {
                response = await _http.GetAsync(url).ConfigureAwait(false);
            }

            using (response)
            {
                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new MagfaException(
                        (int)response.StatusCode,
                        string.Format(Inv,
                            "خطای HTTP {0} ({1}) از سرور مگفا. پاسخ: {2}",
                            (int)response.StatusCode, response.ReasonPhrase, Truncate(text, 500)));
                }

                object parsed;
                try
                {
                    parsed = _json.DeserializeObject(text);
                }
                catch (Exception ex)
                {
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا به‌صورت JSON معتبر نیست: " + Truncate(text, 500),
                        ex);
                }

                IDictionary dict = parsed as IDictionary;
                if (dict == null)
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا قابل خواندن نیست: " + Truncate(text, 500));

                object statusObject;
                if (!TryGetValue(dict, "status", out statusObject) || statusObject == null)
                    throw new MagfaException(
                        MagfaException.InvalidResponseStatus,
                        "پاسخ سرور مگفا بدون فیلد status است: " + Truncate(text, 500));

                int status = Convert.ToInt32(statusObject, Inv);
                if (status != 0)
                    throw new MagfaException(status);

                return dict;
            }
        }

        /// <summary>ساخت بدنه‌ی JSON متد send برای یک دسته از گیرندگان</summary>
        private Dictionary<string, object> BuildSendPayload(SendRequest request, int start, int count)
        {
            string[] recipients = Slice(request.Recipients, start, count);

            string[] messages;
            if (request.Messages.Length == 1)
                messages = Repeat(request.Messages[0], count);
            else
                messages = Slice(request.Messages, start, count);

            string[] senders;
            if (request.Senders == null || request.Senders.Length == 0)
                senders = Repeat(DefaultSender ?? string.Empty, count);
            else if (request.Senders.Length == 1)
                senders = Repeat(request.Senders[0], count);
            else
            {
                if (request.Senders.Length != request.Recipients.Length)
                    throw new ArgumentException(
                        "طول آرایه‌ی senders باید با recipients برابر باشد یا یک مقدار واحد باشد (کد خطای ۱۰۳).",
                        "request.Senders");
                senders = Slice(request.Senders, start, count);
            }

            var payload = new Dictionary<string, object>
            {
                { "recipients", recipients },
                { "messages", messages },
                { "senders", senders }
            };

            if (request.Uids != null && request.Uids.Length > 0)
            {
                if (request.Uids.Length == 1)
                    payload["uids"] = Repeat(request.Uids[0], count);
                else if (request.Uids.Length == request.Recipients.Length)
                    payload["uids"] = Slice(request.Uids, start, count);
                else
                    throw new ArgumentException(
                        "طول آرایه‌ی uids باید با recipients برابر باشد یا یک مقدار واحد باشد (کد خطای ۱۱۰).",
                        "request.Uids");
            }

            if (request.Encodings != null && request.Encodings.Length > 0)
            {
                if (request.Encodings.Length == 1)
                    payload["encodings"] = Repeat(request.Encodings[0], count);
                else if (request.Encodings.Length == request.Recipients.Length)
                    payload["encodings"] = Slice(request.Encodings, start, count);
                else
                    throw new ArgumentException(
                        "طول آرایه‌ی encodings باید با recipients برابر باشد یا یک مقدار واحد باشد (کد خطای ۱۰۹).",
                        "request.Encodings");
            }

            if (request.Udhs != null && request.Udhs.Length > 0)
            {
                if (request.Udhs.Length == 1)
                    payload["udhs"] = Repeat(request.Udhs[0], count);
                else if (request.Udhs.Length == request.Recipients.Length)
                    payload["udhs"] = Slice(request.Udhs, start, count);
                else
                    throw new ArgumentException(
                        "طول آرایه‌ی udhs باید با recipients برابر باشد یا یک مقدار واحد باشد (کد خطای ۱۰۴).",
                        "request.Udhs");
            }

            return payload;
        }

        private static void MergeSendResponse(SendResult result, IDictionary data)
        {
            object messagesObject;
            if (!TryGetValue(data, "messages", out messagesObject) || messagesObject is string) return;

            IEnumerable list = messagesObject as IEnumerable;
            if (list == null) return;

            foreach (object item in list)
            {
                IDictionary dict = item as IDictionary;
                if (dict == null) continue;

                result.Messages.Add(new SendMessageResult
                {
                    Status = GetInt(dict, "status"),
                    Id = GetLong(dict, "id"),
                    UserId = GetLong(dict, "userId"),
                    Parts = GetInt(dict, "parts"),
                    Tariff = GetDecimal(dict, "tariff"),
                    Alphabet = GetString(dict, "alphabet"),
                    Recipient = GetString(dict, "recipient")
                });
            }
        }

        private static void MergeStatusesResponse(StatusesResult result, IDictionary data)
        {
            object dlrsObject;
            if (!TryGetValue(data, "dlrs", out dlrsObject) || dlrsObject is string) return;

            IEnumerable list = dlrsObject as IEnumerable;
            if (list == null) return;

            foreach (object item in list)
            {
                IDictionary dict = item as IDictionary;
                if (dict == null) continue;

                result.Dlrs.Add(new DeliveryStatus
                {
                    Mid = GetLong(dict, "mid", -1),
                    Status = GetInt(dict, "status"),
                    Date = GetString(dict, "date")
                });
            }
        }

        #endregion

        #region ابزارهای کمکی

        private static bool TryGetValue(IDictionary dict, string key, out object value)
        {
            value = null;
            if (dict == null) return false;
            if (!dict.Contains(key)) return false;

            value = dict[key];
            return true;
        }

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

        private static int GetInt(IDictionary dict, string key, int defaultValue = 0)
        {
            object value;
            if (!TryGetValue(dict, key, out value) || value == null) return defaultValue;
            try { return Convert.ToInt32(value, Inv); }
            catch (Exception) { return defaultValue; }
        }

        private static long GetLong(IDictionary dict, string key, long defaultValue = 0)
        {
            object value;
            if (!TryGetValue(dict, key, out value) || value == null) return defaultValue;
            try { return Convert.ToInt64(value, Inv); }
            catch (Exception) { return defaultValue; }
        }

        private static decimal GetDecimal(IDictionary dict, string key, decimal defaultValue = 0)
        {
            object value;
            if (!TryGetValue(dict, key, out value) || value == null) return defaultValue;
            try { return Convert.ToDecimal(value, Inv); }
            catch (Exception) { return defaultValue; }
        }

        private static long? GetNullableLong(IDictionary dict, string key)
        {
            object value;
            if (!TryGetValue(dict, key, out value) || value == null) return null;

            try { return Convert.ToInt64(value, Inv); }
            catch (Exception) { return null; }
        }

        private static string GetString(IDictionary dict, string key)
        {
            object value;
            if (!TryGetValue(dict, key, out value) || value == null) return null;
            return Convert.ToString(value, Inv);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("MagfaClient");
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
