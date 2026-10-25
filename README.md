# هِمّة (Himma)

تطبيق ويب عربي (ومعه الإنجليزية) لمتابعة الأكل والماء والوزن والتمارين، مع لوحة تحكم للمدير.

- الخادم: ASP.NET Core 8 + EF Core 8 + ASP.NET Core Identity (JWT + رمز تجديد في كوكي آمنة).
- قاعدة البيانات: SQLite أثناء التطوير، PostgreSQL في الإنتاج (التبديل بالإعدادات فقط).
- الواجهة: HTML وCSS وJavaScript عادية (بدون build)، يقدّمها الخادم نفسه من `wwwroot`.
- الاختبارات: xUnit + `WebApplicationFactory` (186 اختباراً تعمل على SQLite وعلى PostgreSQL).

تقرير الأمان بنداً بنداً: [SECURITY.md](SECURITY.md).

**الترخيص:** جميع الحقوق محفوظة. الكود منشور للعرض فقط، ولا يُسمح بنسخه أو استخدامه بدون إذن مكتوب. انظر [LICENSE](LICENSE).

**License:** All rights reserved. The code is published for viewing only; no copying or use without written permission. See [LICENSE](LICENSE).

---

## 1. التشغيل على جهازك (Windows)

### 1.1 المتطلبات (مرة واحدة)

1. ثبّت **.NET 8 SDK** (أو أحدث) من https://dotnet.microsoft.com/download، وتأكد أن **.NET 8 Runtime** مثبت.
   تحقق بالأمر:
   ```
   dotnet --list-runtimes
   ```
   يجب أن يظهر `Microsoft.AspNetCore.App 8.x`.
2. ثق بشهادة HTTPS الخاصة بالتطوير:
   ```
   dotnet dev-certs https --trust
   ```
3. ثبّت أداة قاعدة البيانات الخاصة بالمشروع (من مجلد المشروع الرئيسي):
   ```
   dotnet tool restore
   ```

### 1.2 الأسرار (لا تُكتب أبداً في الكود ولا في GitHub)

الأسرار تُحفظ في **user-secrets** داخل ملفك الشخصي على الجهاز، خارج المستودع.
من داخل المجلد `src/Himma.Api`:

```
cd src/Himma.Api
dotnet user-secrets set "Jwt:Key" "<مفتاح عشوائي 32 حرفاً على الأقل>"
dotnet user-secrets set "Admin:Username" "<اسم المدير بالإنجليزي>"
dotnet user-secrets set "Admin:Password" "<كلمة مرور طويلة وقوية>"
```

لتوليد مفتاح عشوائي قوي في PowerShell:
```
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

| السر | ما فائدته | ملاحظات |
| --- | --- | --- |
| `Jwt:Key` | توقيع رموز الدخول | 32 حرفاً على الأقل. تغييره يُبطل رموز الوصول فقط، والمستخدمون يكملون عبر رمز التجديد |
| `Admin:Username` | اسم حساب المدير | يُنشأ تلقائياً عند أول تشغيل. محجوز ولا يمكن التسجيل به |
| `Admin:Password` | كلمة مرور المدير | تُستخدم عند إنشاء الحساب أول مرة فقط؛ بعدها غيّرها من صفحة "حسابي" |
| `ConnectionStrings:Postgres` | قاعدة بيانات الإنتاج | في الإنتاج فقط، كمتغير بيئة |
| `Anthropic:ApiKey` | الذكاء الاصطناعي | للإصدار الثاني |

### 1.3 قاعدة البيانات

في وضع التطوير تُنشأ قاعدة SQLite (`src/Himma.Api/himma.db`) وتُحدَّث تلقائياً عند التشغيل.
وإن أردت تطبيق التحديثات يدوياً:
```
cd src/Himma.Api
dotnet dotnet-ef database update --context SqliteAppDbContext
```

### 1.4 التشغيل

- **Visual Studio**: افتح `Himma.sln`، اختر الملف التعريفي `https`، واضغط **F5**.
- **VS Code أو الطرفية**:
  ```
  cd src/Himma.Api
  dotnet run --launch-profile https
  ```

ثم افتح:
- التطبيق: https://localhost:7099
- لوحة المدير: https://localhost:7099/admin.html (ادخل باسم وكلمة مرور المدير من الخطوة 1.2)
- فحص الخادم: https://localhost:7099/health
- Swagger (في التطوير فقط): https://localhost:7099/swagger

### 1.5 الاختبارات

```
dotnet test
```

لتشغيل نفس الاختبارات على PostgreSQL حقيقي (مثلاً داخل Docker):
```
docker run -d --name himma-pg -e POSTGRES_PASSWORD=<كلمة مرور محلية> -p 127.0.0.1:55432:5432 postgres:16-alpine
```
ثم في PowerShell:
```
$env:HIMMA_TEST_POSTGRES = "Host=127.0.0.1;Port=55432;Username=postgres;Password=<نفس كلمة المرور>"
dotnet test
```
كل مجموعة اختبارات تنشئ قاعدة مؤقتة خاصة بها وتحذفها بعد الانتهاء.

### 1.6 تجربة الروابط يدوياً

الملف `requests.http` فيه كل روابط الـ API، ويعمل مع إضافة **REST Client** في VS Code.
أنشئ ملفاً اسمه `.env` بجانبه (لا يُرفع إلى GitHub):
```
HIMMA_USER=اسم-مستخدم-تجريبي
HIMMA_PASSWORD=كلمة-مرور-تجريبية
HIMMA_ADMIN=اسم-المدير
HIMMA_ADMIN_PASSWORD=كلمة-مرور-المدير
```

---

## 2. الرفع على الإنترنت (Render أو Railway أو Azure)

التطبيق جاهز بصورة Docker (`Dockerfile` في المجلد الرئيسي)، وهي:
- تعمل بمستخدم غير root، وتستمع على المنفذ 8080 (أو `PORT` إن حدّدته المنصة).
- تفعّل وضع الإنتاج وPostgreSQL وقراءة ترويسات الوكيل (`X-Forwarded-*`) تلقائياً.
- لا تحتوي أي سر؛ كل الأسرار تأتي من متغيرات البيئة في المنصة.

### 2.1 متغيرات البيئة المطلوبة

| المتغير | القيمة |
| --- | --- |
| `Jwt__Key` | مفتاح عشوائي جديد (لا تستخدم مفتاح جهازك) |
| `Admin__Username` | اسم المدير |
| `Admin__Password` | كلمة مرور قوية جديدة |
| `ConnectionStrings__Postgres` **أو** `DATABASE_URL` | عنوان قاعدة PostgreSQL. يقبل الصيغتين: `Host=...;Database=...` أو `postgresql://user:pass@host:5432/db` |
| `AllowedHosts` (اختياري، مُستحسن) | نطاق موقعك، مثل `himma.onrender.com` |
| `Push__PublicKey` و`Push__PrivateKey` (اختياري) | مفاتيح التنبيهات (VAPID). اتركهما فارغين: التطبيق ينشئ زوج مفاتيح مرة واحدة ويحفظه في قاعدة البيانات |

> الشرطتان `__` في الأسماء تعني `:` في الإعدادات (مثل `Jwt:Key`).

### 2.2 المهاجرات في الإنتاج

التطبيق **لا يحدّث قاعدة الإنتاج تلقائياً**، ويرفض البدء إن وُجدت تحديثات لم تُطبَّق. لتطبيقها:
```
dotnet Himma.Api.dll --migrate
```
هذا الأمر يطبّق التحديثات، وينشئ حساب المدير والأطعمة والتمارين، ثم يخرج.

### 2.3 Render + Supabase (خطوة بخطوة)

التطبيق على Render (الخطة المجانية)، وقاعدة البيانات PostgreSQL على Supabase (مجانية ولا تنتهي بعد 30 يوماً). الملف `render.yaml` (Blueprint) ينشئ التطبيق ويولّد `Jwt__Key` تلقائياً.

**أولاً: قاعدة البيانات على Supabase**

1. ادخل https://supabase.com → **New project**، المنطقة **Central EU (Frankfurt)** لتكون قريبة من Render.
2. كلمة مرور القاعدة: اضغط **Generate a password** (حروف وأرقام فقط، فلا تحتاج ترميزاً في الرابط) واحفظها في مدير كلمات المرور.
3. **ألغِ تفعيل Data API** (عند الإنشاء في Security options، أو لاحقاً من Project Settings → Data API). هِمّة يتصل بالقاعدة مباشرة ولا يحتاجها، وإبقاؤها مفعّلة قد يكشف الجداول عبر مفتاح `anon` العام.
4. **Connect** → **Connection string** → اختر **Session pooler** (وليس Direct connection، لأن Render لا يدعم IPv6؛ وليس Transaction pooler على المنفذ 6543). انسخ الرابط وضع كلمة المرور مكان `[YOUR-PASSWORD]` وأضف في آخره `?sslmode=require`. الشكل:
   `postgresql://postgres.<المعرّف>:<كلمة المرور>@aws-0-eu-central-1.pooler.supabase.com:5432/postgres?sslmode=require`

**ثانياً: التطبيق على Render**

1. ادخل https://render.com، سجّل الدخول بحساب GitHub، وامنح Render صلاحية الوصول لمستودع `Himma`.
2. **New → Blueprint**، اختر مستودع `Himma`، واكتب عندما يُطلب: `DATABASE_URL` (رابط Supabase) و`Admin__Username` و`Admin__Password`، ثم **Apply**.
3. انتظر حتى ينتهي النشر. التطبيق يطبّق تحديثات قاعدة البيانات وينشئ حساب المدير بنفسه عند التشغيل (`Database__AutoMigrate=true`)، فلا تحتاج أي أمر على جهازك.
4. افتح `https://<الاسم>.onrender.com/health`، ثم سجّل حساباً، ثم ادخل `/admin.html` بحساب المدير.

> رابط قاعدة البيانات سرّ: مكانه **Environment** في Render فقط، لا في الكود ولا في الطرفية ولا في المحادثات.
>
> لو لم يُنشأ حساب المدير: ابحث في **Logs** عن `could not be created`؛ `CommonPassword` يعني أن كلمة المرور سهلة أو تحتوي اسم المستخدم.
>
> السكربت `scripts/migrate-render.ps1` بديل اختياري لو أردت إيقاف `Database__AutoMigrate` وتطبيق التحديثات يدوياً:
> `powershell -ExecutionPolicy Bypass -File .\scripts\migrate-render.ps1`
>
> الخطة المجانية: تطبيق Render ينام بعد 15 دقيقة من عدم الاستخدام (أول فتح يأخذ دقيقة تقريباً)، ومشروع Supabase المجاني **يتوقف مؤقتاً بعد أسبوع بلا أي نشاط** (تعيد تشغيله من لوحة Supabase، والبيانات تبقى).

### 2.4 Railway

1. **New Project → Deploy from GitHub repo** واختر `Himma` (يكتشف `Dockerfile`).
2. **New → Database → PostgreSQL**، ثم في متغيرات الخدمة أضف `DATABASE_URL` كمرجع لمتغير قاعدة البيانات.
3. أضف بقية المتغيرات من الجدول 2.1.
4. في **Settings → Deploy**: ضع Pre-deploy Command = `dotnet Himma.Api.dll --migrate`، وHealthcheck Path = `/health`.
5. **Settings → Networking → Generate Domain** ليصبح لك رابط HTTPS.

### 2.5 Azure App Service

1. أنشئ **Azure Database for PostgreSQL – Flexible Server**، وفعّل الوصول من خدمات Azure.
2. أنشئ **Web App** بنظام Linux ونوع **Container**، وابنِ الصورة من `Dockerfile` (عبر Azure Container Registry أو GitHub Actions).
3. **Configuration → Application settings**: أضف المتغيرات من الجدول 2.1 (ومعها `WEBSITES_PORT=8080`).
4. **Health check**: المسار `/health`.
5. شغّل `--migrate` قبل أول تشغيل (من جهازك بالسكربت في 2.3، أو كأمر بدء مؤقت).
6. فعّل **HTTPS Only** من إعدادات TLS/SSL.

### 2.6 بعد الرفع: قائمة تحقق

- [ ] `https://<موقعك>/health` يعرض `Healthy`.
- [ ] `https://<موقعك>/swagger` يعطي 404 (Swagger مخفي في الإنتاج).
- [ ] فتح الموقع بـ `http://` يحوّلك إلى `https://`.
- [ ] الدخول إلى `/admin.html` بحساب المدير يعمل.
- [ ] غيّر كلمة مرور المدير من صفحة "حسابي" بعد أول دخول.

---

## 3. النسخ الاحتياطي (يومياً)

### الإنتاج (PostgreSQL)

- Render وRailway وAzure تحفظ نسخاً تلقائية في الخطط المدفوعة؛ فعّلها وتأكد من مدة الاحتفاظ.
- نسخة يدوية يومية (من جهاز فيه أدوات PostgreSQL، أو من حاوية `postgres:16`):
  ```
  pg_dump "<External Database URL>" --format=custom --file=himma-2026-10-23.dump
  ```
- الاسترجاع إلى قاعدة فارغة:
  ```
  pg_restore --clean --no-owner --dbname "<Database URL>" himma-2026-10-23.dump
  ```
- احفظ النسخ في مكان آمن ومشفّر، فهي تحتوي بيانات المستخدمين، وجرّب الاسترجاع مرة كل فترة.

### التطوير (SQLite)

أوقف التطبيق ثم انسخ الملف `src/Himma.Api/himma.db` (ومعه `himma.db-wal` إن وُجد).

---

## 4. تدوير المفاتيح وكلمات المرور

| ماذا | كيف | الأثر |
| --- | --- | --- |
| `Jwt:Key` | ضع قيمة جديدة في المنصة (أو user-secrets) وأعد التشغيل | رموز الوصول الحالية تبطل، والتطبيق يجدّدها تلقائياً بكوكي التجديد؛ لا أحد يُجبر على الدخول من جديد |
| إبطال كل الجلسات | غيّر كلمة المرور (للمستخدم) أو أوقف الحساب وأعد تفعيله (للمدير) | يخرج المستخدم من كل أجهزته فوراً |
| كلمة مرور المدير | من صفحة "حسابي" بعد الدخول كمدير | تغيير `Admin__Password` بعد أول تشغيل لا يغيّر شيئاً (الحساب موجود) |
| كلمة مرور قاعدة البيانات | غيّرها في المنصة ثم حدّث `DATABASE_URL` أو `ConnectionStrings__Postgres` وأعد التشغيل | توقف قصير أثناء إعادة التشغيل |
| `Anthropic:ApiKey` (الإصدار الثاني) | أنشئ مفتاحاً جديداً، ضعه في المنصة، ثم احذف القديم من حساب Anthropic | لا شيء على المستخدمين |

إن تسرّب أي سر: غيّره فوراً بنفس الطريقة، ولا تحاول "حذفه" من GitHub فقط.

---

## 5. هيكل المشروع

```
Himma.sln
src/Himma.Api/
  Features/         الروابط حسب الميزة: Auth, Profile, Foods, Meals, Water, Weight, History,
                    Workouts, Tips, Account, Admin
  Domain/           الكيانات، والحسابات (الأهداف، الالتزام)
  Data/             AppDbContext، الإعدادات، البيانات الأولية (Seed)
  Infrastructure/   المصادقة، الأمان، الأخطاء، اللغات، تحديد المعدل، سجل المراجعة
  Migrations/       Sqlite/ و Postgres/ (لكل قاعدة مهاجراتها)
  wwwroot/          الواجهة: index.html (التطبيق) و admin.html (لوحة المدير)
tests/Himma.Tests/  اختبارات الوحدات والتكامل
requests.http       كل الروابط للتجربة اليدوية
SECURITY.md         تقرير الأمان
Dockerfile          صورة الإنتاج
```

### إضافة مهاجرة جديدة (عند تغيير الكيانات)

لكل قاعدة مجلد مهاجرات خاص، فأضِف الاثنتين:
```
cd src/Himma.Api
dotnet dotnet-ef migrations add <الاسم> --context SqliteAppDbContext --output-dir Migrations/Sqlite
dotnet dotnet-ef migrations add <الاسم> --context PostgresAppDbContext --output-dir Migrations/Postgres
```

### اللغات (العربية والإنجليزية)

- الواجهة: اكتب النص هكذا `t('النص العربي')`، وأضف ترجمته في `wwwroot/js/i18n/en.js`.
  مع متغيرات: `t('حذف {name}', { name })`.
- الخادم: كل رسالة للمستخدم تُكتب `Lang.T("عربي", "English")`، والواجهة ترسل اللغة في ترويسة `Accept-Language`.
- البيانات: الأطعمة والتمارين والخطط الجاهزة لها أسماء إنجليزية؛ ما يكتبه المستخدم يبقى بلغته.

### الشعار

ضع ملف `himma-logo.png` في `src/Himma.Api/wwwroot/assets/`، وإلا يُستخدم `himma-logo.svg` الموجود.

### إرشادات التصميم

المجلد `.claude/skills/apple-design` (مثبت بالأمر
`npx skills add https://github.com/emilkowalski/skills --skill apple-design`)
فيه إرشادات للواجهة والحركة لمساعدات البرمجة.
