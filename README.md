# MagfaSms — نسخه ۱ HTTP و نسخه ۲ SOAP

دو کتابخانه‌ی **Class Library** مستقل (هر کدام خروجی‌اش فقط یک فایل `.dll` است) برای کار با سرویس‌های پیامک مگفا روی **.NET Framework 4.8**، بدون هیچ وابستگی خارجی.

| نسخه | پوشه | DLL | سرویس مقصد |
|---|---|---|---|
| **نسخه ۱ — HTTP** | `MagfaSms/` | `MagfaSms.dll` | `https://sms.magfa.com/api/http/sms/v2` |
| **نسخه ۲ — SOAP** | `MagfaSmsSoap/` | `MagfaSmsSoap.dll` | `https://sms.magfa.com/api/soap/sms/v2/server` |

هر دو کلاینت ساختار یکسانی دارند (همان نام متدها و مدل‌ها) تا بتوانید یکی را با کشیدن رفرنس عوض کنید.

---

## فایل‌ها

```
MagfaSms/                 ← نسخه ۱ (HTTP v2)
  MagfaClient.cs
  MagfaModels.cs
  MagfaErrors.cs
  MagfaSms.csproj

MagfaSmsSoap/             ← نسخه ۲ (SOAP v2)
  MagfaSoapClient.cs
  MagfaSoapModels.cs
  MagfaSoapErrors.cs
  SoapMessage.cs
  MagfaSmsSoap.csproj

README.md                 ← همین فایل
```

---

## ۱) بیلد و گرفتن DLL

**با ویژوال استودیو:** سلوشن را باز کنید → `Build` → `Build Solution`.

**با خط فرمان:**
```
msbuild MagfaSms\MagfaSms.csproj /p:Configuration=Release
msbuild MagfaSmsSoap\MagfaSmsSoap.csproj /p:Configuration=Release
```
یا:
```
dotnet build MagfaSms\MagfaSms.csproj -c Release
dotnet build MagfaSmsSoap\MagfaSmsSoap.csproj -c Release
```

خروجی‌ها (هر کدام فقط یک فایل):

```
MagfaSms\bin\Release\net48\MagfaSms.dll
MagfaSmsSoap\bin\Release\net48\MagfaSmsSoap.dll
```

---

## ۲) افزودن به پروژه‌ی خود

1. در Solution Explorer روی `References` (یا `Dependencies`) راست‌کلیک → `Add Reference…`
2. تب `Browse` → `Browse…` → فایل DLL مدنظر را انتخاب → `OK`

**اگر پروژه‌ی شما SDK-style است** و خطای «مرجع داده نشده» گرفتید، این رفرنس‌ها را اضافه کنید:
```
System.Net.Http
System.Web.Extensions      ← فقط برای نسخه HTTP
System.Xml.Linq            ← فقط برای نسخه SOAP
```
(همه‌ی این‌ها اسمبلی‌های خود دات‌نت فریم‌ورک‌اند؛ نیازی به NuGet ندارند.)

---

## ۳) اعتبارنامه‌ها

سه مدارک ورود حساب مگفا (از پنل `https://messaging.magfa.com/ui/`):

| پارامتر | منبع |
|---|---|
| `username` | نام‌کاربری پنل |
| `password` | **رمز عبور سرویس** — پنل ← «مدیریت حساب» ← «رمز عبور و امنیت» |
| `domain` | دامنه‌ی حساب — برای اکثر حساب‌ها `magfa` |

در هر دو نسخه، این‌ها به‌صورت `username/domain:password` در هدر `Authorization` (Basic) ترکیب می‌شوند.

> اگر دامنه اشتباه باشد: کد `19`؛ اگر نام‌کاربری/رمز اشتباه باشد: کد `18`؛
> اگر سرویس برای حساب فعال نباشد: کد `22`.

### نمونه‌ی امن برای نگه‌داشتن رمز (app.config)

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
```

---

# نسخه ۱ — HTTP  (`MagfaSms.dll`)

```csharp
using MagfaSms;

using (var client = new MagfaClient(username, password, domain, sender))
{
    // ۱) مانده اعتبار
    BalanceResult balance = await client.GetBalanceAsync();
    Console.WriteLine("اعتبار: " + balance.Balance + " ریال");

    // ۲) ارسال
    SendResult send = await client.SendAsync(new SendRequest
    {
        Recipients = new[] { "09120000000" },
        Messages   = new[] { "سلام، این یک پیامک تستی است" },
        Uids       = new long[] { 1001 }    // اختیاری
    });

    foreach (SendMessageResult m in send.Messages)
        Console.WriteLine(m.Recipient + " | mid=" + m.Id + " | status=" + m.Status
                          + " | parts=" + m.Parts + " | tariff=" + m.Tariff
                          + " | alphabet=" + m.Alphabet);

    // ۳) وضعیت تحویل
    StatusesResult st = await client.GetStatusesAsync(new long[] { send.Messages[0].Id });
    foreach (DeliveryStatus d in st.Dlrs)
        Console.WriteLine(d.Mid + " → " + d.StatusText + " (" + d.Status + ")");

    // ۴) mid از روی uid
    MidResult mid = await client.GetMidAsync(1001);
    Console.WriteLine("mid=" + mid.Mid);

    // ۵) پیامک‌های ورودی
    ReceivedMessagesResult inbox = await client.GetReceivedMessagesAsync(20);
    foreach (ReceivedMessage m in inbox.Messages)
        Console.WriteLine(m.Date + " | از " + m.SenderNumber + " : " + m.Body);
}
```

### ساخت کلاینت HTTP
```csharp
using (var client = new MagfaClient("myUsername", "myServicePass", "magfa", "3000xxxxx"))
```
اگر پورت/آدرس را می‌خواهید عوض کنید، سازنده‌ی ۵ آرگومانی را با `baseUrl` استفاده کنید.

### متدهای HTTP

| متد مگفا | متد کلاینت | توضیح |
|---|---|---|
| `balance` | `GetBalanceAsync()` | مانده اعتبار (`Balance` از نوع `long?`) |
| `send` | `SendAsync(request)` | ارسال (با دسته‌بندی خودکار ۱۰۰تایی) |
| `mid` | `GetMidAsync(uid)` | شناسه‌ی یکتای پیامک از روی uid |
| `statuses` | `GetStatusesAsync(mids)` | وضعیت تحویل (حداکثر ۱۰۰ در هر درخواست) |
| `messages` | `GetReceivedMessagesAsync(count)` | پیامک‌های ورودی |

---

# نسخه ۲ — SOAP  (`MagfaSmsSoap.dll`)

```csharp
using MagfaSms.Soap;

using (var client = new MagfaSoapClient(username, password, domain, sender))
{
    // ۱) مانده اعتبار
    BalanceResult balance = await client.GetBalanceAsync();
    Console.WriteLine("اعتبار: " + balance.Balance);

    // ۲) ارسال
    SendResult send = await client.SendAsync(new SendRequest
    {
        Messages   = new[] { "سلام، این یک پیامک تستی است" },
        Senders    = new[] { "3000xxxxx" },
        Recipients = new[] { "09120000000" },
        Uids       = new long[] { 1001 },        // اختیاری
        Encodings  = new[] { (int)MagfaEncoding.Persian }, // اختیاری
        Udhs       = new string[] { "" },        // اختیاری
        Priorities = new[] { 0 }                 // اختیاری (در مگفا استفاده نمی‌شود)
    });

    foreach (SendMessageResult m in send.Messages)
        Console.WriteLine(m.Recipient + " | mid=" + m.Id + " | status=" + m.Status);

    // ۳) وضعیت تحویل
    StatusesResult st = await client.GetStatusesAsync(new long[] { send.Messages[0].Id });
    foreach (DeliveryStatus d in st.Dlrs)
        Console.WriteLine(d.Mid + " → " + d.StatusText + " (" + d.Status + ")");

    // ۴) mid از روی uid
    MidResult mid = await client.GetMidAsync(1001);

    // ۵) پیامک‌های ورودی (اختیاری: فقط یک شماره‌ی خاص)
    ReceivedMessagesResult inbox = await client.GetReceivedMessagesAsync(20);
    ReceivedMessagesResult ofOne = await client.GetReceivedMessagesAsync(20, "3000xxxxx");
}
```

### ساخت کلاینت SOAP
```csharp
using (var client = new MagfaSoapClient("myUsername", "myServicePass", "magfa", "3000xxxxx"))
```
سازنده‌ی ۵ آرگومانی آدرس سرویس را عوض می‌کند (`endpoint`).

### متدهای SOAP

| متد مگفا | متد کلاینت | توضیح |
|---|---|---|
| `balance` | `GetBalanceAsync()` | مانده اعتبار (`Balance` از نوع `decimal?`) |
| `send` | `SendAsync(request)` | ارسال (با دسته‌بندی خودکار ۱۰۰تایی) |
| `mid` | `GetMidAsync(uid)` | شناسه‌ی یکتای پیامک از روی uid |
| `statuses` | `GetStatusesAsync(mids)` | وضعیت تحویل |
| `messages` | `GetReceivedMessagesAsync(count, shortNumber)` | پیامک‌های ورودی |

### نکات فنی نسخه SOAP
* پاکت ارسالی **SOAP 1.1 / RPC-Literal** با `namespace` عملیات `webservice.magfa.com` ساخته می‌شود
  (بر اساس WSDL سرویس). برای متد `balance` مثلاً:
  ```xml
  <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
    <soap:Body>
      <balance xmlns="webservice.magfa.com" />
    </soap:Body>
  </soap:Envelope>
  ```
* پاسخ **بدون توجه به namespace** خوانده می‌شود (بر اساس نام عنصر)، پس به تفاوت‌های کوچک در خروجی سرور حساس نیست.
* برای دیباگ، پراپرتی‌های `LastRequestXml` و `LastResponseXml` آخرین درخواست/پاسخ خام را نگه می‌دارند.
* پاسخ‌های GZIP به‌صورت خودکار بازگشایی می‌شوند.

### روش جایگزین و تضمینی (Add Service Reference)
اگر با نسخه‌ی دست‌ساز به خطای SOAP خوردید، مسیر استاندارد مایکروسافت و مگفا این است:

1. در ویژوال استودیو روی پروژه → `Add → Service Reference…` → `Advanced…` → `Add Web Reference…`
2. آدرس WSDL را بگذارید: `https://sms.magfa.com/api/soap/sms/v2/server?wsdl`
3. نامی مثل `MagfaWebService` بدهید و `Add Reference`
4. در کد:
```csharp
var service = new MagfaWebService.MagfaSoapServer();
service.Credentials = new System.Net.NetworkCredential(username + "/" + domain, password);
service.EnableDecompression = true;

var result = service.balance();
if (result.status != 0) Console.WriteLine(result.status);
else Console.WriteLine(result.balance);
```

---

## ۴) مدیریت خطا (در هر دو نسخه)

اگر سرور کد `status` غیر از صفر برگرداند (یا پاسخ نامعتبر/خطای HTTP/SOAP باشد)، یک
`MagfaException` با متن فارسی و پراپرتی `Status` پرتاب می‌شود:

```csharp
try
{
    var r = await client.SendAsync("09120000000", "سلام");
}
catch (MagfaException ex)
{
    Console.WriteLine("کد: " + ex.Status);
    Console.WriteLine("متن: " + ex.Message);
}
```

نکته: در متد `send`، اگر یک پیامک خاص ناموفق باشد، کل درخواست خطا نمی‌دهد؛ نتیجه‌ی هر پیامک در
`SendResult.Messages[*].Status` می‌آید (در نسخه HTTP با `MagfaErrors.GetMessage(status)` و در نسخه SOAP
با `MagfaSoapErrors.GetMessage(status)` متن فارسی‌اش را بگیرید).

---

## ۵) جدول کدهای خطا (در هر دو نسخه یکسان)

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

## ۶) نکات و محدودیت‌ها

* **حداکثر ۱۰۰ گیرنده** در هر درخواست `send` و **۱۰۰ شناسه** در `statuses` — هر دو کلاینت خودشان دسته‌بندی می‌کنند.
* **فرمت شماره‌ها:** فرستنده `3000xxxxxx` / `983000xxxxxx` / `+983000xxxxxx` ؛
  گیرنده `09xxxxxxxxx` / `989xxxxxxxxx` / `+989xxxxxxxxx` / `9xxxxxxxxx`.
* **متن پیامک:** UTF-8. حداکثر: لاتین ۱۶۰ کاراکتر، فارسی ۷۰ کاراکتر، باینری ۱۴۰ بایت
  (چندبخشی: لاتین ۱۵۳ و فارسی ۶۷ در هر بخش، سقف ۲۶۵ بخش).
* **`messages`:** اگر `count` داده نشود یا بیش از ۱۰۰ باشد، ۱۰۰ در نظر گرفته می‌شود.
* **`uid`:** برای جلوگیری از ارسال تکراری در زمان TIMEOUT — بعداً با `GetMidAsync(uid)` چک کنید.
* **`Timeout`:** پیش‌فرض ۶۰ ثانیه، قابل تغییر است.
* **`userId` در خروجی `send`:** در پیامک موفق به‌صورت عدد و در ناموفق به‌صورت رشته (`"1223"`) برمی‌گردد؛
  کلاینت هر دو را درست به `long` تبدیل می‌کند.
* کلاینت را در پایان کار `Dispose` کنید (`using`).
* اگر خطای `29` گرفتید، IP سرور شما باید در پنل مگفا («آی‌پی‌های معتبر») ثبت شده باشد.
* اگر خطای `22` گرفتید، سرویس موردنظر (HTTP یا SOAP) برای حساب فعال نشده — با واحد فروش مگفا تماس بگیرید.
