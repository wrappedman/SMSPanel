using System;
using System.Collections.Generic;

namespace MagfaSms
{
    /// <summary>درخواست ارسال پیامک (معادل بدنه JSON متد send)</summary>
    public class SendRequest
    {
        /// <summary>گیرندگان — الزامی. حداکثر ۱۰۰ عدد در هر درخواست (بیشتر به‌صورت خودکار دسته‌بندی می‌شود).</summary>
        public string[] Recipients { get; set; }

        /// <summary>
        /// متن پیامک‌ها — الزامی. یا دقیقاً به اندازه‌ی Recipients است (هر گیرنده پیام خودش را می‌گیرد)
        /// و یا یک مقدار واحد که به همه‌ی گیرندگان ارسال می‌شود.
        /// </summary>
        public string[] Messages { get; set; }

        /// <summary>
        /// فرستندگان — اختیاری. اگر خالی باشد از شماره‌ی پیش‌فرض کلاینت استفاده می‌شود.
        /// می‌تواند یک مقدار واحد یا به اندازه‌ی Recipients باشد.
        /// </summary>
        public string[] Senders { get; set; }

        /// <summary>شناسه‌ی یکتای کاربر (uid) برای پیگیری بعداً با متد mid — اختیاری.</summary>
        public long[] Uids { get; set; }

        /// <summary>نوع انکودینگ پیامک‌ها — اختیاری (مقادیر MagfaEncoding). پیش‌فرض: تشخیص خودکار.</summary>
        public int[] Encodings { get; set; }

        /// <summary>UDH برای ارسال‌های پیشرفته — اختیاری.</summary>
        public string[] Udhs { get; set; }
    }

    /// <summary>خروجی متد balance</summary>
    public class BalanceResult
    {
        public int Status { get; set; }

        /// <summary>مانده اعتبار (long طبق مستندات). در صورت خطا null.</summary>
        public long? Balance { get; set; }
    }

    /// <summary>نتیجه‌ی پردازش یک پیامک در خروجی متد send</summary>
    public class SendMessageResult
    {
        /// <summary>۰ یعنی موفق؛ غیر از آن کد خطای همان پیامک است.</summary>
        public int Status { get; set; }

        /// <summary>شناسه‌ی یکتای پیامک (mid) — برای پیگیری وضعیت با متد statuses.</summary>
        public long Id { get; set; }

        public long UserId { get; set; }

        /// <summary>تعداد بخش‌های پیامک (پیامک طولانی چند بخشی می‌شود).</summary>
        public int Parts { get; set; }

        /// <summary>تعرفه (هزینه) پیامک.</summary>
        public decimal Tariff { get; set; }

        /// <summary>DEFAULT برای لاتین و UCS2 برای فارسی.</summary>
        public string Alphabet { get; set; }

        public string Recipient { get; set; }
    }

    /// <summary>خروجی متد send</summary>
    public class SendResult
    {
        public int Status { get; set; }

        public List<SendMessageResult> Messages { get; set; }

        public int SuccessCount { get; set; }

        public int FailCount { get; set; }

        public SendResult()
        {
            Messages = new List<SendMessageResult>();
        }
    }

    /// <summary>خروجی متد mid</summary>
    public class MidResult
    {
        public int Status { get; set; }

        /// <summary>شناسه‌ی یکتای پیامک یا ۱- در صورت یافت نشدن.</summary>
        public long Mid { get; set; }
    }

    /// <summary>وضعیت تحویل یک پیامک</summary>
    public class DeliveryStatus
    {
        public long Mid { get; set; }

        /// <summary>یکی از مقادیر DeliveryStatusCodes</summary>
        public int Status { get; set; }

        /// <summary>تاریخ به فرمت yyyy-mm-dd hh:mm:ss</summary>
        public string Date { get; set; }

        /// <summary>متن فارسی وضعیت</summary>
        public string StatusText
        {
            get { return DeliveryStatusCodes.GetText(Status); }
        }
    }

    /// <summary>خروجی متد statuses</summary>
    public class StatusesResult
    {
        public int Status { get; set; }

        public List<DeliveryStatus> Dlrs { get; set; }

        public StatusesResult()
        {
            Dlrs = new List<DeliveryStatus>();
        }
    }

    /// <summary>یک پیامک ورودی</summary>
    public class ReceivedMessage
    {
        /// <summary>متن پیام</summary>
        public string Body { get; set; }

        /// <summary>فرستنده</summary>
        public string SenderNumber { get; set; }

        /// <summary>گیرنده (شماره‌ی شما)</summary>
        public string RecipientNumber { get; set; }

        /// <summary>تاریخ به فرمت yyyy-mm-dd hh:mm:ss</summary>
        public string Date { get; set; }
    }

    /// <summary>خروجی متد messages</summary>
    public class ReceivedMessagesResult
    {
        public int Status { get; set; }

        public List<ReceivedMessage> Messages { get; set; }

        public ReceivedMessagesResult()
        {
            Messages = new List<ReceivedMessage>();
        }
    }

    /// <summary>خطای درخواست به سرویس مگفا</summary>
    public class MagfaException : Exception
    {
        /// <summary>
        /// کد status برگردانده‌شده از سرور مگفا. برای خطاهای سطح HTTP، کد وضعیت HTTP است
        /// و برای خطاهای پاسخ نامعتبر، مقدار ثابت MagfaException.InvalidResponseStatus.
        /// </summary>
        public int Status { get; private set; }

        /// <summary>مقدار Status برای خطاهایی که از سمت سرور مگفا کد status ندارند (پاسخ نامعتبر/غیرJSON)</summary>
        public const int InvalidResponseStatus = -1000;

        public MagfaException(int status)
            : this(status, MagfaErrors.GetMessage(status), null)
        {
        }

        public MagfaException(int status, string message)
            : this(status, message, null)
        {
        }

        public MagfaException(int status, string message, Exception inner)
            : base(message, inner)
        {
            Status = status;
        }
    }
}
