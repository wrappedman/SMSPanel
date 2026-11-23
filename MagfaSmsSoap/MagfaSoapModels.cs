using System;
using System.Collections.Generic;

namespace MagfaSms.Soap
{
    /// <summary>
    /// درخواست ارسال پیامک برای سرویس SOAP v2
    /// (معادل متد send سرویس https://sms.magfa.com/api/soap/sms/v2/server)
    /// </summary>
    public class SendRequest
    {
        /// <summary>متن پیامک‌ها — الزامی</summary>
        public string[] Messages { get; set; }

        /// <summary>فرستنده‌ها — الزامی</summary>
        public string[] Senders { get; set; }

        /// <summary>گیرنده‌ها — الزامی (حداکثر ۱۰۰ در هر درخواست)</summary>
        public string[] Recipients { get; set; }

        /// <summary>شناسه‌ی یکتای کاربر (uid) — اختیاری</summary>
        public long[] Uids { get; set; }

        /// <summary>انکودینگ‌ها (مقادیر MagfaEncoding) — اختیاری</summary>
        public int[] Encodings { get; set; }

        /// <summary>UDHها — اختیاری</summary>
        public string[] Udhs { get; set; }

        /// <summary>اولویت پیامک‌ها — اختیاری (در مگفا استفاده نمی‌شود)</summary>
        public int[] Priorities { get; set; }
    }

    /// <summary>خروجی متد balance — موجودیت CreditResult</summary>
    public class BalanceResult
    {
        public int Status { get; set; }

        /// <summary>مانده اعتبار. در صورت خطا null.</summary>
        public decimal? Balance { get; set; }
    }

    /// <summary>نتیجه‌ی پردازش یک پیامک — موجودیت SendMessage</summary>
    public class SendMessageResult
    {
        public int Status { get; set; }

        /// <summary>شناسه‌ی یکتای پیامک (mid)</summary>
        public long Id { get; set; }

        public long UserId { get; set; }

        public int Parts { get; set; }

        public decimal Tariff { get; set; }

        /// <summary>DEFAULT برای لاتین و UCS2 برای فارسی</summary>
        public string Alphabet { get; set; }

        public string Recipient { get; set; }
    }

    /// <summary>خروجی متد send — موجودیت SendResult</summary>
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

    /// <summary>خروجی متد mid — موجودیت MessageIdResult</summary>
    public class MidResult
    {
        public int Status { get; set; }

        /// <summary>شناسه‌ی یکتای پیامک (یا ۱- اگر یافت نشود)</summary>
        public long Mid { get; set; }
    }

    /// <summary>وضعیت تحویل — موجودیت DeliveryStatus</summary>
    public class DeliveryStatus
    {
        public long Mid { get; set; }

        /// <summary>یکی از مقادیر DeliveryStatusCodes</summary>
        public int Status { get; set; }

        /// <summary>تاریخ به فرمت yyyy-mm-dd hh:mm:ss</summary>
        public string Date { get; set; }

        /// <summary>سامانه‌ی پیام‌رسان (smsc) — در برخی پاسخ‌ها موجود است</summary>
        public string Smsc { get; set; }

        /// <summary>متن فارسی وضعیت</summary>
        public string StatusText
        {
            get { return DeliveryStatusCodes.GetText(Status); }
        }
    }

    /// <summary>خروجی متد statuses — موجودیت DeliveryResult</summary>
    public class StatusesResult
    {
        public int Status { get; set; }

        public List<DeliveryStatus> Dlrs { get; set; }

        public StatusesResult()
        {
            Dlrs = new List<DeliveryStatus>();
        }
    }

    /// <summary>یک پیامک ورودی — موجودیت DatedCustomerReturnIncomingFormat</summary>
    public class ReceivedMessage
    {
        public string Body { get; set; }

        public string SenderNumber { get; set; }

        public string RecipientNumber { get; set; }

        public string Date { get; set; }
    }

    /// <summary>خروجی متد messages — موجودیت MessagesResult</summary>
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

        /// <summary>مقدار Status برای خطاهایی که از سرور مگفا کد status ندارند</summary>
        public const int InvalidResponseStatus = -1000;

        public MagfaException(int status)
            : this(status, MagfaSoapErrors.GetMessage(status), null)
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

    /// <summary>مقادیر انکودینگ پیامک</summary>
    public enum MagfaEncoding
    {
        Auto = 0,
        Persian = 2,
        EightBit = 5,
        Binary = 6
    }
}
