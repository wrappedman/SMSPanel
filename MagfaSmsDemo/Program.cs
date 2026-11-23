using System;
using System.Text;
using System.Threading.Tasks;
using MagfaSms;

namespace MagfaSmsDemo
{
    /// <summary>
    /// نمونه اجرایی کلاینت MagfaSms
    ///
    /// روش اجرا:
    ///   1) مقادیر را به‌صورت متغیر محیطی بدهید:
    ///        set MAGFA_USERNAME=...
    ///        set MAGFA_PASSWORD=...
    ///        set MAGFA_DOMAIN=...
    ///        set MAGFA_SENDER=3000xxxxx
    ///        set MAGFA_RECIPIENT=0912xxxxxxx
    ///      (در PowerShell به‌جای set از $env:MAGFA_USERNAME="..." استفاده کنید)
    ///   2) MagfaSmsDemo.exe را اجرا کنید. اگر متغیر محیطی ندارید، برنامه از شما سؤال می‌کند.
    ///      شماره گیرنده را هم می‌توانید آرگومان خط فرمان بدهید:  MagfaSmsDemo.exe 0912xxxxxxx
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                RunAsync(args).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("خطا: " + ex.Message);
                Console.ResetColor();

                var magfaError = ex as MagfaException;
                if (magfaError != null)
                {
                    Console.WriteLine("کد status: " + magfaError.Status);
                }

                if (ex.InnerException != null)
                {
                    Console.WriteLine("جزئیات بیشتر: " + ex.InnerException.Message);
                }

                return 1;
            }
        }

        private static async Task RunAsync(string[] args)
        {
            string username = ReadSetting("MAGFA_USERNAME", "نام‌کاربری مگفا");
            string password = ReadSetting("MAGFA_PASSWORD", "رمز عبور سرویس مگفا");
            string domain = ReadSetting("MAGFA_DOMAIN", "دامنه مگفا");
            string sender = ReadSetting("MAGFA_SENDER", "شماره‌ی فرستنده (مثلاً 3000xxxxx)");

            string recipient = args.Length > 0
                ? args[0]
                : ReadSetting("MAGFA_RECIPIENT", "شماره‌ی گیرنده (مثلاً 0912xxxxxxx)");

            string baseUrl = Environment.GetEnvironmentVariable("MAGFA_BASE_URL");
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = null;

            using (var client = new MagfaClient(username, password, domain, sender, baseUrl))
            {
                // ---------------------------------------------------------------
                // ۱) مانده اعتبار
                // ---------------------------------------------------------------
                Console.WriteLine(">> دریافت مانده اعتبار ...");
                BalanceResult balance = await client.GetBalanceAsync().ConfigureAwait(false);
                Console.WriteLine("   مانده اعتبار: " + (balance.Balance.HasValue
                    ? balance.Balance.Value.ToString("0") + " ریال"
                    : "نامشخص"));
                Console.WriteLine();

                // ---------------------------------------------------------------
                // ۲) ارسال پیامک (با uid برای اینکه بعداً بتوانیم mid را از آن بگیریم)
                // ---------------------------------------------------------------
                Console.WriteLine(">> ارسال پیامک به " + recipient + " ...");
                SendResult send = await client.SendAsync(new SendRequest
                {
                    Recipients = new[] { recipient },
                    Messages = new[] { "تست ارسال پیامک از کلاینت C# مگفا" },
                    Uids = new long[] { 123456 }
                }).ConfigureAwait(false);

                Console.WriteLine("   موفق: " + send.SuccessCount + " | ناموفق: " + send.FailCount);

                long mid = -1;
                foreach (SendMessageResult message in send.Messages)
                {
                    if (message.Status == 0)
                    {
                        mid = message.Id;
                        Console.WriteLine("   mid=" + message.Id
                            + " | parts=" + message.Parts
                            + " | tariff=" + message.Tariff
                            + " | alphabet=" + message.Alphabet
                            + " | recipient=" + message.Recipient);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("   خطای پیامک " + message.Recipient + ": "
                            + message.Status + " - " + MagfaErrors.GetMessage(message.Status));
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();

                // ---------------------------------------------------------------
                // ۳) پیگیری وضعیت تحویل
                // ---------------------------------------------------------------
                if (mid > 0)
                {
                    Console.WriteLine(">> پیگیری وضعیت تحویل mid=" + mid + " ...");
                    StatusesResult statuses = await client.GetStatusesAsync(new long[] { mid }).ConfigureAwait(false);
                    foreach (DeliveryStatus status in statuses.Dlrs)
                    {
                        Console.WriteLine("   mid=" + status.Mid
                            + " | status=" + status.Status
                            + " (" + status.StatusText + ")"
                            + " | date=" + status.Date);
                    }
                    Console.WriteLine();
                }

                // ---------------------------------------------------------------
                // ۴) دریافت mid از روی uid
                // ---------------------------------------------------------------
                Console.WriteLine(">> دریافت mid از uid=123456 ...");
                try
                {
                    MidResult midResult = await client.GetMidAsync(123456).ConfigureAwait(false);
                    Console.WriteLine("   mid=" + midResult.Mid);
                }
                catch (MagfaException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("   " + ex.Status + " - " + ex.Message);
                    Console.ResetColor();
                }
                Console.WriteLine();

                // ---------------------------------------------------------------
                // ۵) پیامک‌های ورودی
                // ---------------------------------------------------------------
                Console.WriteLine(">> دریافت ۲۰ پیامک ورودی ...");
                ReceivedMessagesResult received = await client.GetReceivedMessagesAsync(20).ConfigureAwait(false);
                Console.WriteLine("   تعداد: " + received.Messages.Count);
                foreach (ReceivedMessage message in received.Messages)
                {
                    Console.WriteLine("   " + message.Date
                        + " | از " + message.SenderNumber
                        + " به " + message.RecipientNumber
                        + " | " + message.Body);
                }
            }

            Console.WriteLine();
            Console.WriteLine("پایان.");
        }

        private static string ReadSetting(string environmentVariable, string prompt)
        {
            string value = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            Console.Write(prompt + ": ");
            return Console.ReadLine();
        }
    }
}
