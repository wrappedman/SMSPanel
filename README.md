# MagfaSms — کتابخانه‌ی C# سرویس HTTP v2 مگفا

یک **کتابخانه‌ی Class Library** (خروجی: فقط یک فایل `MagfaSms.dll`) برای کار با
[سرویس HTTP v2 مگفا](https://messaging.magfa.com/ui/?public/wiki/api/http_v2) روی **.NET Framework 4.8**.

مناسب برای برنامه‌های WinForms، WPF، ASP.NET، کنسولی و سرویس‌های ویندوزی.
**بدون هیچ وابستگی خارجی** (فقط اسمبلی‌های داخلی دات‌نت فریم‌ورک).

---

## فایل‌های پروژه

| مسیر | نقش |
|---|---|
| `MagfaSms/MagfaClient.cs` | کلاینت اصلی و توابع سرویس |
| `MagfaSms/MagfaModels.cs` | مدل‌های داده و کلاس `MagfaException` |
| `MagfaSms/MagfaErrors.cs` | جدول کدها و وضعیت‌ها |
| `MagfaSms/MagfaSms.csproj` | فایل پروژه |
| `README.md` | همین فایل راهنما |

---

## ۱) بیلد و گرفتن DLL

**با ویژوال استودیو:**
سلوشن را باز کنید → `Build` → `Build Solution` (یا `Ctrl+Shift+B`).

**با خط فرمان:**
```
msbuild MagfaSms\MagfaSms.csproj /p:Configuration=Release
```
یا اگر `dotnet` دارید:
```
dotnet build MagfaSms\MagfaSms.csproj -c Release
```

خروجی (فقط یک فایل):

```
MagfaSms\bin\Debug\net48\MagfaSms.dll       ← بیلد Debug
MagfaSms\bin\Release\net48\MagfaSms.dll     ← بیلد Release
```

> همین یک `MagfaSms.dll` را کپی می‌کنید و در برنامه‌های مختلف رفرنس می‌دهید.

---

## ۲) افزودن به پروژه‌ی خود

**روش ساده (رفرنس مستقیم به DLL):**
1. در Solution Explorer روی `References` (یا `Dependencies`) راست‌کلیک → `Add Reference…`
2. تب `Browse` → `Browse…` → فایل `MagfaSms.dll` را انتخاب → `OK`

**اگر پروژه‌ی خودتان SDK-style است** و خطای «مرجع داده نشده» گرفتید، این دو رفرنس را هم اضافه کنید:
```
System.Net.Http
System.Web.Extensions
```
(این‌ها اسمبلی‌های خودِ دات‌نت فریم‌ورک‌اند و نیازی به NuGet ندارند.)

**روش دوم (اضافه‌کردن پروژه به سلوشن):** در سلوشن خود `Add → Existing Project` و انتخاب `MagfaSms.csproj`، بعد از پروژه‌ی خودتان `Add → Project Reference`.

---

## ۳) نمونه‌ی کامل (از صفر تا اجرا)

```csharp
using System;
using System.Threading.Tasks;
using MagfaSms;                 // ← این using بعد از دادن رفرنس کار می‌کند

namespace MyApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // ---------- ساخت کلاینت ----------
            using (var client = new MagfaClient(
                username:      "myUsername",     // نام‌کاربری پنل مگفا
                password:      "myServicePass",  // رمز «عبور سرویس» (نه رمز ورود پنل)
                domain:        "magfa",          // دامنه‌ی حساب (اکثر حساب‌ها: magfa)
                defaultSender: "3000xxxxx"))     // شماره‌ی فرستنده
            {
                // ---------- ۱) مانده اعتبار ----------
                BalanceResult balance = await client.GetBalanceAsync();
                Console.WriteLine("اعتبار: " + balance.Balance + " ریال");

                // ---------- ۲) ارسال پیامک ----------
                SendResult send = await client.SendAsync(new SendRequest
                {
                    Recipients = new[] { "09120000000" },
                    Messages   = new[] { "سلام، این یک پیامک تستی است" },
                    Uids       = new long[] { 1001 }   // اختیاری: برای جلوگیری از ارسال تکراری
                });

                foreach (SendMessageResult m in send.Messages)
                {
                    Console.WriteLine("گیرنده: " + m.Recipient
                                    + " | mid: " + m.Id
                                    + " | وضعیت: " + m.Status
                                    + " | بخش‌ها: " + m.Parts
                                    + " | تعرفه: " + m.Tariff
                                    + " | الفبا: " + m.Alphabet);
                }

                long mid = send.Messages[0].Id;

                // ---------- ۳) پیگیری وضعیت تحویل ----------
                StatusesResult statuses = await client.GetStatusesAsync(new long[] { mid });
                foreach (DeliveryStatus d in statuses.Dlrs)
                    Console.WriteLine("mid " + d.Mid + " → " + d.Status + " (" + d.StatusText + ")");

                // ---------- ۴) گرفتن mid از روی uid ----------
                MidResult midResult = await client.GetMidAsync(1001);
                Console.WriteLine("uid 1001 → mid " + midResult.Mid);

                // ---------- ۵) پیامک‌های ورودی ----------
                ReceivedMessagesResult inbox = await client.GetReceivedMessagesAsync(20);
                foreach (ReceivedMessage m in inbox.Messages)
                    Console.WriteLine(m.Date + " | از " + m.SenderNumber + " : " + m.Body);
            }
        }
    }
}
```

> **برنامه‌ی کنسولی باشد:** اگر `Main` شما `async Task` نیست یا نسخه‌ی C# شما کمتر از ۷.۱ است،
> `static void Main` بسازید و `Main` را این‌طور بنویسید:
> `RunAsync().GetAwaiter().GetResult();`
> یا از نسخه‌های همگام استفاده کنید: `client.GetBalance()` ،`client.Send(...)` ،`client.GetStatuses(...)` و…

---

## ۴) نمونه به‌ازای هر تابع

### ساخت کلاینت
```csharp
using (var client = new MagfaClient("myUsername", "myServicePass", "magfa", "3000xxxxx"))
{
    // ...
}
```

### `GetBalanceAsync` — مانده اعتبار
```csharp
BalanceResult r = await client.GetBalanceAsync();
// r.Balance  → long?   (ریال)
```

### `SendAsync` — ارسال

**یک پیام به یک گیرنده:**
```csharp
SendResult r = await client.SendAsync("09120000000", "متن پیامک");
```

**یک پیام به چند گیرنده (یک به چند):**
```csharp
SendResult r = await client.SendAsync(
    new[] { "09120000000", "09121111111", "09351234567" },
    "متن یکسان برای همه");
```

**پیام متفاوت برای هر گیرنده (چند به چند):**
```csharp
SendResult r = await client.SendAsync(new SendRequest
{
    Recipients = new[] { "09120000000", "09121111111" },
    Messages   = new[] { "متن برای اول", "متن برای دوم" },
    Senders    = new[] { "3000xxxxx", "3000yyyyy" },   // اختیاری
    Uids       = new long[] { 1001, 1002 },            // اختیاری
    Encodings  = new[] { (int)MagfaEncoding.Persian }, // اختیاری، پیش‌فرض تشخیص خودکار
    Udhs       = new string[] { "" }                   // اختیاری
});
```

**خروجی ارسال:**
```csharp
Console.WriteLine("موفق: " + r.SuccessCount + " | ناموفق: " + r.FailCount);
foreach (SendMessageResult m in r.Messages)
{
    if (m.Status == 0)
        Console.WriteLine("mid=" + m.Id + " parts=" + m.Parts + " tariff=" + m.Tariff + " alphabet=" + m.Alphabet);
    else
        Console.WriteLine("خطا " + m.Status + ": " + MagfaErrors.GetMessage(m.Status));
}
```

> **بیش از ۱۰۰ گیرنده:** لازم نیست خودتان دسته‌بندی کنید؛ کلاینت خودش به دسته‌های ۱۰۰تایی تقسیم می‌کند
> و نتیجه‌ی همه را در یک `SendResult` برمی‌گاند.

### `GetMidAsync` — شناسه‌ی یکتای پیامک از روی uid
```csharp
MidResult r = await client.GetMidAsync(1001);
// r.Mid  → long  (یا -1 اگر یافت نشود)
```
کاربرد: اگر هنگام ارسال با TIMEOUT مواجه شدید، با همان `uid` بپرسید آیا پیامک قبلاً رفته یا نه تا
از ارسال دوباره جلوگیری کنید.

### `GetStatusesAsync` — وضعیت تحویل
```csharp
StatusesResult r = await client.GetStatusesAsync(new long[] { 111111111, 222222222 });
foreach (DeliveryStatus d in r.Dlrs)
    Console.WriteLine(d.Mid + " → " + d.StatusText + " (" + d.Status + ") در " + d.Date);
```
حداکثر ۱۰۰ شناسه در هر درخواست (کلاینت خودش دسته‌بندی می‌کند).

### `GetReceivedMessagesAsync` — پیامک‌های ورودی
```csharp
ReceivedMessagesResult r = await client.GetReceivedMessagesAsync(20);
// count پیش‌فرض = 100 ؛ اگر کمتر از ۱ یا بیش از ۱۰۰ بدهید، ۱۰۰ در نظر گرفته می‌شود.
foreach (ReceivedMessage m in r.Messages)
    Console.WriteLine(m.Date + " | " + m.SenderNumber + " → " + m.RecipientNumber + " | " + m.Body);
```

---

## ۵) اعتبارنامه‌ها و دامنه

سه مدارک ورود حساب مگفا (از پنل `https://messaging.magfa.com/ui/`):

| پارامتر کلاینت | منبع |
|---|---|
| `username` | نام‌کاربری پنل |
| `password` | **رمز عبور سرویس** — پنل ← «مدیریت حساب» ← «رمز عبور و امنیت» |
| `domain` | دامنه‌ی حساب — برای اکثر حساب‌ها `magfa` |

در درخواست HTTP این‌ها به‌صورت `username/domain:password` در هدر `Authorization` (Basic) ترکیب می‌شوند —
که کلاینت خودش انجام می‌دهد.

**اگر دامنه اشتباه باشد:** خطای `19`؛ اگر نام‌کاربری/رمز اشتباه باشد: خطای `18`.

### نکته‌ی امنیتی
رمز عبور را در کد هاردکد نکنید. به‌عنوان نمونه در `app.config`:

```xml
<appSettings>
  <add key="Magfa:Username" value="myUsername" />
  <add key="Magfa:Password" value="myServicePass" />
  <add key="Magfa:Domain"   value="magfa" />
  <add key="Magfa:Sender"   value="3000xxxxx" />
</appSettings>
```

```csharp
using System.Configuration;

string username = ConfigurationManager.AppSettings["Magfa:Username"];
string password = ConfigurationManager.AppSettings["Magfa:Password"];
string domain   = ConfigurationManager.AppSettings["Magfa:Domain"];
string sender   = ConfigurationManager.AppSettings["Magfa:Sender"];

var client = new MagfaClient(username, password, domain, sender);
```

---

## ۶) مدیریت خطا

اگر سرور کد `status` غیر از صفر برگرداند، یا پاسخ نامعتبر/خطای HTTP باشد، یک
`MagfaException` با متن فارسی و پراپرتی `Status` پرتاب می‌شود:

```csharp
try
{
    var r = await client.SendAsync("09120000000", "سلام");
}
catch (MagfaException ex)
{
    Console.WriteLine("کد: " + ex.Status);
    Console.WriteLine("متن: " + ex.Message);   // مثلاً «مانده اعتبار ریالی کافی نیست»
}
```

نکته: در متد `send`، اگر یک پیامک خاص ناموفق باشد، کل درخواست خطا **نمی‌دهد**؛
نتیجه‌ی هر پیامک در `SendResult.Messages[*].Status` می‌آید و متن فارسی‌اش با
`MagfaErrors.GetMessage(status)` قابل دسترس است.

---

## ۷) جدول کدهای خطا (کامل)

| کد | معنی |
|---|---|
| 1 | شماره گیرنده نادرست است |
| 2 | شماره فرستنده نادرست است |
| 3 | پارامتر encoding نامعتبر است |
| 4 | پارامتر mclass نامعتبر است |
| 6 | پارامتر UDH نامعتبر است |
| 8 | زمان ارسال، خارج از بازه‌ی مجاز ارسال تبلیغاتی (۷ الی ۲۲) است |
| 13 | محتویات پیامک (ترکیب UDH و متن) خالی است |
| 14 | مانده اعتبار ریالی کافی نیست |
| 15 | خطای داخلی سرور — ارسال مجدد درخواست |
| 16 | حساب غیرفعال است |
| 17 | حساب منقضی شده است |
| 18 | نام کاربری و یا کلمه عبور نامعتبر است |
| 19 | درخواست معتبر نیست (ترکیب نام کاربری/رمز/دامنه اشتباه) |
| 20 | شماره فرستنده به حساب تعلق ندارد |
| 22 | این سرویس برای حساب فعال نشده است |
| 23 | امکان پردازش وجود ندارد — ارسال مجدد درخواست |
| 24 | شناسه پیامک معتبر نیست (اشتباه یا گذشت بیش از یک روز) |
| 25 | نام متد درخواستی معتبر نیست |
| 27 | گیرنده در لیست سیاه اپراتور است |
| 28 | گیرنده بر اساس پیش‌شماره در مگفا مسدود است |
| 29 | آدرس IP مبدأ اجازه‌ی دسترسی ندارد |
| 30 | تعداد بخش‌های پیامک بیش از ۲۶۵ است |
| 31 | فرمت JSON ارسالی صحیح نیست |
| 33 | مشترک دریافت پیامک از این سرشماره را مسدود کرده است (لغو ۱۱) |
| 34 | اطلاعات تاییدشده برای این شماره وجود ندارد |
| 35 | وجود کاراکتر نامعتبر در متن پیامک |
| 101 | طول آرایه‌ی messageBodies با گیرندگان تطابق ندارد |
| 102 | طول آرایه‌ی messageClass با گیرندگان تطابق ندارد |
| 103 | طول آرایه‌ی senderNumbers با گیرندگان تطابق ندارد |
| 104 | طول آرایه‌ی udhs با گیرندگان تطابق ندارد |
| 105 | طول آرایه‌ی priorities با گیرندگان تطابق ندارد |
| 106 | آرایه‌ی گیرندگان خالی است |
| 107 | طول آرایه‌ی گیرندگان بیش از حد مجاز است |
| 108 | آرایه‌ی فرستندگان خالی است |
| 109 | طول آرایه‌ی encoding با گیرندگان تطابق ندارد |
| 110 | طول آرایه‌ی checkingMessageIds با گیرندگان تطابق ندارد |

### کدهای وضعیت تحویل (`statuses`)

| کد | معنی |
|---|---|
| -1 | شناسه موجود نیست (نادرست یا گذشت بیش از ۲۴ ساعت) |
| 0 | وضعیتی دریافت نشده |
| 1 | رسیده به گوشی |
| 2 | نرسیده به گوشی |
| 8 | رسیده به مخابرات |
| 16 | نرسیده به مخابرات |

---

## ۸) نکات و محدودیت‌ها

* **حداکثر ۱۰۰ گیرنده** در هر درخواست `send` (کلاینت خودش دسته‌بندی می‌کند).
* **حداکثر ۱۰۰ شناسه** در هر درخواست `statuses` (کلاینت خودش دسته‌بندی می‌کند).
* **فرمت شماره‌ها:** فرستنده `3000xxxxxx` / `983000xxxxxx` / `+983000xxxxxx` ؛
  گیرنده `09xxxxxxxxx` / `989xxxxxxxxx` / `+989xxxxxxxxx` / `9xxxxxxxxx`.
* **متن پیامک:** UTF-8 (کلاینت خودش این‌طور ارسال می‌کند). حداکثر: لاتین ۱۶۰ کاراکتر،
  فارسی ۷۰ کاراکتر، باینری ۱۴۰ بایت. در پیامک چندبخشی: لاتین ۱۵۳ و فارسی ۶۷ کاراکتر در هر بخش (سقف ۲۶۵ بخش).
* **فشرده‌سازی:** پاسخ‌های GZIP به‌صورت خودکار بازگشایی می‌شوند.
* ** Timeout:** پیش‌فرض ۶۰ ثانیه است؛ با پراپرتی `client.Timeout` قابل تغییر است.
* **`userId` در خروجی `send`:** در پیامک موفق به‌صورت عدد و در ناموفق به‌صورت رشته (`"1223"`) برمی‌گردد؛
  کلاینت هر دو را درست به `long` تبدیل می‌کند.
* `MagfaClient` را در پایان کار `Dispose` کنید (`using`).
* اگر خطای `29` گرفتید، IP سرور شما باید در پنل مگفا در بخش «آی‌پی‌های معتبر» ثبت شده باشد.
