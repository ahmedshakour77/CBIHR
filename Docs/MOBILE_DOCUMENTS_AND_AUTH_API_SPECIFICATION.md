# دليل واجهات البرمجة لتطبيق الهاتف (Mobile API Specification)
## نظام الموارد البشرية - الصلاحيات والمستمسكات الرسمية الثلاثة
**التاريخ:** 2026-10-20  
**الإصدار:** v1.1.0  
**الحالة:** معتمد ومنفذ بالكامل في السيرفر (Production Ready)

---

## 📌 الفهرس العام
1. [نظرة عامة والتوثيق الأمني](#1-نظرة-عامة-والتوثيق-الأمني)
2. [الأدوار والصلاحيات (Roles & Permissions) وطرق تسجيل الدخول](#2-الأدوار-والصلاحيات-roles--permissions-وطرق-تسجيل-الدخول)
   - [أ. تسجيل الدخول العادي (`POST /api/auth/login`)](#أ-تسجيل-الدخول-العادي-post-apiauthlogin)
   - [ب. تسجيل الدخول عبر مايكروسوفت (`POST /api/auth/microsoft-login`)](#ب-تسجيل-الدخول-عبر-مايكروسوفت-post-apiauthmicrosoft-login)
   - [ج. تسجيل الدخول عبر الدومين / الأكتف ديركتوري (`POST /api/auth/ad-login`)](#ج-تسجيل-الدخول-عبر-الدومين--الأكتف-ديركتوري-post-apiauthad-login)
   - [د. مسار الملف الشخصي الحالي للموظف (`GET /api/employees/me`)](#د-مسار-الملف-الشخصي-الحالي-للموظف-get-apiemployeesme)
   - [هـ. مسار سياق حساب المستخدم الحالي (`GET /api/auth/me`)](#هـ-مسار-سياق-حساب-المستخدم-الحالي-get-apiauthme)
3. [استرجاع المستمسكات الرسمية الثلاثة دفعة واحدة](#3-استرجاع-المستمسكات-الرسمية-الثلاثة-دفعة-واحدة)
   - [أ. استرجاع مستمسكات الموظف الحالي (خدمة ذاتية) (`GET /api/documents/me`)](#أ-استرجاع-مستمسكات-الموظف-الحالي-خدمة-ذاتية-get-apidocumentsme)
   - [ب. استرجاع مستمسكات موظف محدد (أدمن / مدراء) (`GET /api/documents/{employeeID}`)](#ب-استرجاع-مستمسكات-موظف-محدد-أدمن--مدراء-get-apidocumentsemployeeid)
4. [واجهات إضافة وتحديث المستمسكات (Add & Update)](#4-واجهات-إضافة-وتحديث-المستمسكات-add--update)
   - [أ. جواز السفر (إلكتروني أو عادي) (`POST /api/documents/passport`)](#أ-جواز-السفر-إلكتروني-أو-عادي-post-apidocumentspassport)
   - [ب. بطاقة السكن (دائماً عادي ورقي) (`POST /api/documents/residence`)](#ب-بطاقة-السكن-دائما-عادي-ورقي-post-apidocumentsresidence)
   - [ج. البطاقة الوطنية الموحدة (دائماً إلكتروني بشريحة ذكية) (`POST /api/documents/national-card`)](#ج-البطاقة-الوطنية-الموحدة-دائما-إلكتروني-بشريحة-ذكية-post-apidocumentsnational-card)
5. [مسارات استعراض وتنزيل الصور والوثائق](#5-مسارات-استعراض-وتنزيل-الصور-والوثائق)
6. [جدول حالات الاستجابة والأخطاء (HTTP Status Codes)](#6-جدول-حالات-الاستجابة-والأخطاء-http-status-codes)

---

## 1. نظرة عامة والتوثيق الأمني

* **طريقة المصادقة:** Bearer Token (JWT).
* يتم إرسال التوكن في ترويسة الطلب لجميع المسارات المحمية:
  ```http
  Authorization: Bearer <ACCESS_TOKEN>
  ```
* **تصنيف المستمسكات الثلاثة المعتمدة:**
  1. **البطاقة الوطنية الموحدة:** دائماً إلكترونية وتعتمد شريحة ذكية وباركود/MRZ (`isElectronic: true`).
  2. **بطاقة السكن:** دائماً ورقية عادية بدون شريحة (`isElectronic: false`).
  3. **جواز السفر العراقي:** يحتوي على تصنيفين صريحين:
     * **جواز إلكتروني (بشريحة ذكية):** `isElectronic: true`، و `passportCategory: "إلكتروني"`.
     * **جواز عادي (مقروء آلياً ورقي فقط):** `isElectronic: false`، و `passportCategory: "عادي"`.

---

## 2. الأدوار والصلاحيات (Roles & Permissions)

### أ. تسجيل الدخول العادي (`POST /api/auth/login`)
* **Endpoint:** `POST /api/auth/login`
* **المصادقة:** عام (مفتوح).
* **Request Body:**
```json
{
  "email": "ahmed.ali@company.iq",
  "password": "StrongPassword123!",
  "deviceId": "c1f7b029-9e85-4ab2-b2d9-174829104829",
  "deviceModel": "Samsung Galaxy S24 Ultra",
  "operatingSystem": "Android 14",
  "appVersion": "1.2.0"
}
```

* **Response Body (`200 OK`):**
```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 86400,
  "refreshToken": "CfDJ8...",
  "employeeID": "1041001",
  "name": "أحمد علي حسين",
  "roles": [
    "HR_Admin",
    "Employee"
  ],
  "isAdmin": true,
  "permissions": [
    "VIEW_ALL_EMPLOYEES",
    "MANAGE_DOCUMENTS",
    "VERIFY_DOCUMENTS",
    "EXPORT_REPORTS"
  ]
}
```

---

### ب. تسجيل الدخول عبر مايكروسوفت (`POST /api/auth/microsoft-login`)
* **Endpoint:** `POST /api/auth/microsoft-login`
* **المصادقة:** عام (مفتوح).
* **Request Body:**
```json
{
  "idToken": "eyJhbGciOi... (Microsoft Entra ID Token)",
  "deviceId": "c1f7b029-9e85-4ab2-b2d9-174829104829",
  "deviceModel": "iPhone 15 Pro",
  "operatingSystem": "iOS 18.0",
  "appVersion": "1.2.0"
}
```
* **Response Body (`200 OK`):** يرجع نفس بنية الرد أعلاه تماماً متضمنة التوكن والصلاحيات والأدوار واسم الموظف ورقم وظيفته.

---

### ج. تسجيل الدخول عبر الدومين / الأكتف ديركتوري (`POST /api/auth/ad-login`)
* **Endpoint:** `POST /api/auth/ad-login`
* **المصادقة:** عام (مفتوح).
* **الوصف:** مصادقة اسم المستخدم وكلمة المرور الخاصة بحساب الموظف على الـ Active Directory (دومين المؤسسة `aml.lan`). يتم التحقق آلياً من صحة الحساب في الدومين، ومطابقته مع سجلات الموظفين، ثم إنشاء وتحديث حساب الـ JWT وربطه بالهاتف المعتمد.

#### تفاصيل الحقول:
| الحقل | النوع | إلزامي؟ | الوصف |
| :--- | :--- | :---: | :--- |
| `userName` | String | **نعم\*** | اسم مستخدم الدومين (مثال: `ahmed.ali` أو `AML\ahmed.ali` أو `ahmed.ali@aml.lan`) |
| `email` | String | بديل\* | البريد الإلكتروني للموظف على الدومين (يمكن إرساله بدلاً من `userName`) |
| `password` | String | **نعم** | كلمة مرور الدومين (Active Directory Password) |
| `deviceId` | String | لا | المعرف الفريد لهاتف الموظف (UUID) لربط الحساب بالجهاز |
| `deviceModel` | String | لا | نوع وطراز الهاتف (مثال: `Samsung Galaxy S24` أو `iPhone 15 Pro`) |
| `operatingSystem`| String | لا | نظام التشغيل (مثال: `Android 14` أو `iOS 18.0`) |
| `appVersion` | String | لا | إصدار تطبيق الهاتف (مثال: `1.2.0`) |

* **Request Body:**
```json
{
  "userName": "ahmed.ali",
  "password": "DomainPassword123!",
  "deviceId": "c1f7b029-9e85-4ab2-b2d9-174829104829",
  "deviceModel": "Samsung Galaxy S24 Ultra",
  "operatingSystem": "Android 14",
  "appVersion": "1.2.0"
}
```

* **Response Body (`200 OK`):**
```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 86400,
  "refreshToken": "CfDJ8...",
  "employeeID": "1041001",
  "name": "أحمد علي حسين",
  "roles": [
    "HR_Admin",
    "Employee"
  ],
  "isAdmin": true,
  "permissions": [
    "VIEW_ALL_EMPLOYEES",
    "MANAGE_DOCUMENTS",
    "VERIFY_DOCUMENTS",
    "EXPORT_REPORTS"
  ]
}
```

---

### د. مسار الملف الشخصي الحالي للموظف (`GET /api/employees/me`)
* **Endpoint:** `GET /api/employees/me`
* **المصادقة:** `Bearer Token` مطلوب.
* **Response Body (`200 OK`):**
```json
{
  "employeeID": "1041001",
  "name": "أحمد علي حسين",
  "roles": [
    "HR_Admin",
    "Employee"
  ],
  "isAdmin": true,
  "permissions": [
    "VIEW_ALL_EMPLOYEES",
    "MANAGE_DOCUMENTS",
    "VERIFY_DOCUMENTS",
    "EXPORT_REPORTS"
  ],
  "id": 142,
  "jobTitle": "مسؤول تدقيق الوثائق والمستمسكات",
  "department": "إدارة الموارد البشرية",
  "formation": "المقر العام",
  "administrativeUnit": "شعبة الأضابير والوثائق",
  "email": "ahmed.ali@company.iq",
  "phoneNumber": "07701234567",
  "hireDate": "2015-04-01",
  "serviceStatus": "مستمر بالخدمة"
}
```

---

### هـ. مسار سياق حساب المستخدم الحالي (`GET /api/auth/me`)
* **Endpoint:** `GET /api/auth/me`
* **المصادقة:** `Bearer Token` مطلوب.
* **Response Body (`200 OK`):**
```json
{
  "userId": "d748f2a1-5721-49b0-9513-cf8129302194",
  "userName": "ahmed.ali",
  "email": "ahmed.ali@company.iq",
  "phoneNumber": "07701234567",
  "employeeId": "1041001",
  "employeeID": "1041001",
  "name": "أحمد علي حسين",
  "roles": [
    "HR_Admin",
    "Employee"
  ],
  "isAdmin": true,
  "permissions": [
    "VIEW_ALL_EMPLOYEES",
    "MANAGE_DOCUMENTS",
    "VERIFY_DOCUMENTS",
    "EXPORT_REPORTS"
  ],
  "hasProfilePhoto": true
}
```

---

## 3. استرجاع المستمسكات الرسمية الثلاثة دفعة واحدة

### أ. استرجاع مستمسكات الموظف الحالي (خدمة ذاتية) (`GET /api/documents/me`)
* **Endpoint:** `GET /api/documents/me`
* **المصادقة:** أي موظف مسجل دخوله بالتطبيق.

### ب. استرجاع مستمسكات موظف محدد (أدمن / مدراء) (`GET /api/documents/{employeeID}`)
* **Endpoint:** `GET /api/documents/{employeeID}`
* **المصادقة:** يتطلب صلاحية إدارية (Staff / Admin).

---

### نموذج الاستجابة الشاملة الكاملة (`200 OK`):
```json
{
  "employeeID": "1041001",
  "employeeName": "أحمد علي حسين",
  "hasNationalCard": true,
  "hasResidence": true,
  "hasPassport": true,
  "allDocumentsComplete": true,
  "nationalCard": {
    "id": 55,
    "documentType": "البطاقة الوطنية الموحدة",
    "isElectronic": true,
    "documentFormat": "إلكتروني (شريحة ذكية)",
    "documentNumber": "0034189214",
    "nationalIdNumber": "199012345678",
    "fullNameAr": "أحمد علي حسين",
    "firstNameEn": "AHMED",
    "lastNameEn": "ALI",
    "fullNameEn": "AHMED ALI HUSSEIN",
    "dateOfBirth": "1990-05-14",
    "gender": "ذكر",
    "nationality": "عراقي",
    "issuingState": "IRQ",
    "issueDate": "2020-01-10",
    "expiryDate": "2030-01-10",
    "rawMrz": "I<IRQ0034189214<5<<<<<<<<<<<<<\n9005142M3001108IRQ<<<<<<<<<<<8\nAHMED<<ALI<HUSSEIN<<<<<<<<<<<<",
    "hasFrontImage": true,
    "frontImageUrl": "/api/national-cards/55/front-image",
    "hasBackImage": true,
    "backImageUrl": "/api/national-cards/55/back-image",
    "hasBiometricFace": true,
    "biometricFaceUrl": "/api/national-cards/55/biometric-face"
  },
  "residence": {
    "id": 88,
    "documentType": "بطاقة السكن",
    "isElectronic": false,
    "documentFormat": "عادي (ورقي)",
    "formNumber": "7482910",
    "formDate": "2021-03-20",
    "informationOffice": "مكتب معلومات الكرادة",
    "familyHeadName": "علي حسين",
    "governorateId": 1,
    "governorate": "بغداد",
    "district": "الرصافة",
    "mahalla": "903",
    "zuqaq": "14",
    "dar": "22",
    "housingType": "ملك",
    "complexName": null,
    "buildingNumber": null,
    "floorNumber": null,
    "apartmentNumber": null,
    "latitude": 33.3152,
    "longitude": 44.3661,
    "locationUrl": "https://maps.google.com/?q=33.3152,44.3661",
    "hasCardFront": true,
    "cardFrontUrl": "/api/residence/88/card/front",
    "hasCardBack": true,
    "cardBackUrl": "/api/residence/88/card/back"
  },
  "passport": {
    "id": 42,
    "documentType": "جواز السفر",
    "isElectronic": true,
    "passportCategory": "إلكتروني",
    "passportType": "عادي (P)",
    "passportNumber": "B12345678",
    "countryCode": "IRQ",
    "issuingCountry": "العراق",
    "issuingAuthority": "مديرية شؤون الجوازات",
    "nationalIdNumber": "199012345678",
    "fullNameAr": "أحمد علي حسين",
    "fullNameEn": "AHMED ALI HUSSEIN",
    "motherName": "فاطمة حسن",
    "placeOfBirth": "بغداد",
    "dateOfBirth": "1990-05-14",
    "gender": "ذكر",
    "issueDate": "2023-06-01",
    "expiryDate": "2031-06-01",
    "rawMrz": "P<IRQALI<<AHMED<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\nB123456782IRQ9005142M3106015199012345678<<02",
    "hasImage": true,
    "imageUrl": "/api/passport/42/image",
    "hasBioPhoto": true,
    "biometricPhotoUrl": "/api/passport/42/photo",
    "notes": null
  }
}
```

> **ملاحظة هامة لتطبيق الموبايل:**  
> إذا لم يكن المستمسك مسجلاً بعد، فستكون قيمته `null` في الكائن المقابل، وستكون قيمة العلم المقابل (`hasNationalCard`, `hasResidence`, `hasPassport`) هي `false`.

---

## 4. واجهات إضافة وتحديث المستمسكات (Add & Update)

### آلية العمل (Self-Service vs Admin):
1. **الموظف العادي (الخدمة الذاتية):**
   * يمكنه ترك حقل `employeeID` فارغاً (`null` أو غير موجود بالـ JSON)، أو يضع رقمه الخاص.
   * سيقوم السيرفر تلقائياً بربط الوثيقة مع حسابه فقط.
2. **المدير / الأدمن (HR Admin / Staff):**
   * يرسل قيمة `employeeID` الخاصة بالموظف المستهدف، ويقوم السيرفر بتحديث وثيقة ذلك الموظف.

---

### أ. جواز السفر (إلكتروني أو عادي) (`POST /api/documents/passport`)
* **Endpoint:** `POST /api/documents/passport`
* **المصادقة:** `Bearer Token`

#### تفاصيل الحقول:
| الحقل | النوع | إلزامي؟ | الوصف |
| :--- | :--- | :---: | :--- |
| `employeeID` | String | اختياري | الرقم الوظيفي (إلزامي فقط إذا كان الأدمن يحدث لموظف آخر) |
| `passportNumber` | String | **نعم** | رقم الجواز (مثال: `B12345678` أو `A9876543`) |
| `isElectronic` | Boolean | **نعم** | `true` إذا كان الجواز إلكترونياً جديداً، أو `false` إذا كان عادياً ورprop |
| `passportType` | String | لا | نوع الجواز: `"عادي (P)"` أو `"دبلوماسي (D)"` أو `"خدمة (S)"` أو `"خاص (Sp)"` |
| `countryCode` | String | لا | رمز الدولة (الافتراضي: `"IRQ"`) |
| `issuingCountry` | String | لا | الدولة المصدرة (الافتراضي: `"العراق"`) |
| `issuingAuthority`| String | لا | جهة الإصدار (مثال: `"مديرية شؤون الجوازات"`) |
| `nationalIdNumber`| String | لا | الرقم الوطني الموحد (12 رقم) |
| `fullNameAr` | String | لا | الاسم الكامل باللغة العربية |
| `fullNameEn` | String | لا | الاسم الكامل بالإنجليزية كما يظهر بالجواز |
| `motherName` | String | لا | اسم الأم الثلاثي |
| `placeOfBirth` | String | لا | مكان الولادة (مثال: `"بغداد"`) |
| `dateOfBirth` | String (YYYY-MM-DD) | لا | تاريخ الولادة |
| `gender` | String | لا | الجنس: `"ذكر"` أو `"أنثى"` |
| `issueDate` | String (YYYY-MM-DD) | لا | تاريخ الإصدار |
| `expiryDate` | String (YYYY-MM-DD) | لا | تاريخ انتهاء الصلاحية |
| `rawMrz` | String | لا | الأسطر المقروءة آلياً (MRZ) سطرين |
| `passportImageBase64` | String | لا | صورة صفحة البيانات كاملة (JPEG/PNG تشفير Base64) |
| `biometricPhotoBase64` | String | لا | صورة الوجه البيومترية المستخرجة (Base64) |
| `notes` | String | لا | ملاحظات إضافية |

#### Request Body مثال:
```json
{
  "employeeID": "1041001",
  "passportNumber": "B12345678",
  "isElectronic": true,
  "passportType": "عادي (P)",
  "countryCode": "IRQ",
  "issuingCountry": "العراق",
  "issuingAuthority": "مديرية شؤون الجوازات",
  "nationalIdNumber": "199012345678",
  "fullNameAr": "أحمد علي حسين",
  "fullNameEn": "AHMED ALI HUSSEIN",
  "motherName": "فاطمة حسن",
  "placeOfBirth": "بغداد",
  "dateOfBirth": "1990-05-14",
  "gender": "ذكر",
  "issueDate": "2023-06-01",
  "expiryDate": "2031-06-01",
  "rawMrz": "P<IRQALI<<AHMED<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\nB123456782IRQ9005142M3106015199012345678<<02",
  "passportImageBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "biometricPhotoBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "notes": "تم المسح عبر قارئ NFC بالهاتف"
}
```

#### Response Body (`200 OK`):
```json
{
  "success": true,
  "message": "تم حفظ وتحديث بيانات جواز السفر بنجاح.",
  "isElectronic": true,
  "passportCategory": "إلكتروني",
  "passportId": 42,
  "passportNumber": "B12345678"
}
```

---

### ب. بطاقة السكن (دائماً عادي ورقي) (`POST /api/documents/residence`)
* **Endpoint:** `POST /api/documents/residence`
* **المصادقة:** `Bearer Token`

#### تفاصيل الحقول:
| الحقل | النوع | إلزامي؟ | الوصف |
| :--- | :--- | :---: | :--- |
| `employeeID` | String | اختياري | الرقم الوظيفي (خاص بالأدمن لتحديد الموظف) |
| `formNumber` | String | لا | رقم استمارة بطاقة السكن |
| `formDate` | String (YYYY-MM-DD) | لا | تاريخ تنظيم بطاقة السكن |
| `informationOffice` | String | لا | مكتب المعلومات المنظم للبطاقة |
| `familyHeadName` | String | لا | اسم رب الأسرة |
| `governorateId` | Integer | لا | معرف المحافظة (1=بغداد، 2=البصرة، إلخ) |
| `governorateName`| String | لا | اسم المحافظة |
| `district` | String | لا | القضاء أو الناحية |
| `mahalla` | String | لا | رقم المحلة |
| `zuqaq` | String | لا | رقم الزقاق |
| `dar` | String | لا | رقم الدار |
| `housingType` | String | لا | نوع السكن: `"ملك"`، `"إيجار"`، `"مجمع سكني"` |
| `complexName` | String | لا | اسم المجمع السكني (في حال السكن بمجمع) |
| `buildingNumber`| String | لا | رقم العمارة |
| `floorNumber` | String | لا | الطابق |
| `apartmentNumber`| String | لا | رقم الشقة |
| `latitude` | Double | لا | الإحداثي الجغرافي لخط العرض (GPS) |
| `longitude` | Double | لا | الإحداثي الجغرافي لخط الطول (GPS) |
| `locationUrl` | String | لا | رابط الموقع على خرائط Google Maps |
| `cardFrontBase64` | String | لا | صورة الوجه الأمامي لبطاقة السكن (Base64) |
| `cardBackBase64` | String | لا | صورة الوجه الخلفي لبطاقة السكن (Base64) |

#### Request Body مثال:
```json
{
  "employeeID": "1041001",
  "formNumber": "7482910",
  "formDate": "2021-03-20",
  "informationOffice": "مكتب معلومات الكرادة",
  "familyHeadName": "علي حسين",
  "governorateId": 1,
  "governorateName": "بغداد",
  "district": "الكرادة",
  "mahalla": "903",
  "zuqaq": "14",
  "dar": "22",
  "housingType": "ملك",
  "latitude": 33.3152,
  "longitude": 44.3661,
  "locationUrl": "https://maps.google.com/?q=33.3152,44.3661",
  "cardFrontBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "cardBackBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD..."
}
```

#### Response Body (`200 OK`):
```json
{
  "success": true,
  "message": "تم حفظ وتحديث بيانات بطاقة السكن بنجاح.",
  "isElectronic": false,
  "documentFormat": "عادي (ورقي)",
  "residenceId": 88,
  "formNumber": "7482910",
  "informationOffice": "مكتب معلومات الكرادة"
}
```

---

### ج. البطاقة الوطنية الموحدة (دائماً إلكتروني بشريحة ذكية) (`POST /api/documents/national-card`)
* **Endpoint:** `POST /api/documents/national-card`
* **المصادقة:** `Bearer Token`

#### تفاصيل الحقول:
| الحقل | النوع | إلزامي؟ | الوصف |
| :--- | :--- | :---: | :--- |
| `employeeID` | String | اختياري | الرقم الوظيفي (خاص بالأدمن) |
| `documentNumber` | String | **نعم** | رقم الوثيقة / رقم البطاقة الموحدة (10 خانات) |
| `nationalIdNumber`| String | لا | الرقم الوطني الموحد (12 خانة) |
| `fullNameAr` | String | لا | الاسم الكامل بالعربية |
| `firstNameEn` | String | لا | الاسم اللاتيني الأول |
| `lastNameEn` | String | لا | اللقب أو الاسم اللاتيني الأخير |
| `fullNameEn` | String | لا | الاسم اللاتيني الكامل |
| `dateOfBirth` | String (YYYY-MM-DD) | لا | تاريخ الولادة |
| `gender` | String | لا | الجنس (`"ذكر"` أو `"أنثى"`) |
| `nationality` | String | لا | الجنسية (الافتراضي: `"عراقي"`) |
| `issuingState` | String | لا | رمز الدولة المصدرة (الافتراضي: `"IRQ"`) |
| `issueDate` | String (YYYY-MM-DD) | لا | تاريخ الإصدار |
| `expiryDate` | String (YYYY-MM-DD) | لا | تاريخ انتهاء الصلاحية |
| `rawMrz` | String | لا | أسطر الـ MRZ الثلاثة (معيار TD1) |
| `biometricFaceBase64` | String | لا | الصورة الشخصية البيومترية المستخرجة من شريحة الـ RFID (DG2 Face Image) |
| `frontImageBase64` | String | لا | صورة الوجه الأمامي للبطاقة (Base64) |
| `backImageBase64` | String | لا | صورة الوجه الخلفي للبطاقة (Base64) |
| `isPassiveAuthenticationValid` | Boolean | لا | نتيجة التحقق الرقمي السلبي (SOD) من الشريحة الذكية |

#### Request Body مثال:
```json
{
  "employeeID": "1041001",
  "documentNumber": "0034189214",
  "nationalIdNumber": "199012345678",
  "fullNameAr": "أحمد علي حسين",
  "firstNameEn": "AHMED",
  "lastNameEn": "ALI",
  "fullNameEn": "AHMED ALI HUSSEIN",
  "dateOfBirth": "1990-05-14",
  "gender": "ذكر",
  "nationality": "عراقي",
  "issuingState": "IRQ",
  "issueDate": "2020-01-10",
  "expiryDate": "2030-01-10",
  "rawMrz": "I<IRQ0034189214<5<<<<<<<<<<<<<\n9005142M3001108IRQ<<<<<<<<<<<8\nAHMED<<ALI<HUSSEIN<<<<<<<<<<<<",
  "biometricFaceBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "frontImageBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "backImageBase64": "/9j/4AAQSkZJRgABAQEAYABgAAD...",
  "isPassiveAuthenticationValid": true
}
```

#### Response Body (`200 OK`):
```json
{
  "success": true,
  "message": "تم حفظ وتحديث بيانات البطاقة الوطنية الموحدة بنجاح.",
  "isElectronic": true,
  "documentFormat": "إلكتروني (شريحة ذكية)",
  "card": {
    "id": 55,
    "documentNumber": "0034189214",
    "nationalIdNumber": "199012345678",
    "fullNameAr": "أحمد علي حسين",
    "isElectronic": true
  }
}
```

---

## 5. مسارات استعراض وتنزيل الصور والوثائق

يمكن لتطبيق الموبايل عرض وتنزيل الصور مباشرة عبر روابط الـ HTTP مع ترويسة المصادقة:

### 1. البطاقة الوطنية الموحدة
* **صورة الوجه الأمامي:** `GET /api/national-cards/{id}/front-image`
* **صورة الوجه الخلفي:** `GET /api/national-cards/{id}/back-image`
* **صورة الوجه البيومترية (من الشريحة):** `GET /api/national-cards/{id}/biometric-face`

### 2. بطاقة السكن
* **صورة الوجه الأمامي:** `GET /api/residence/{id}/card/front`
* **صورة الوجه الخلفي:** `GET /api/residence/{id}/card/back`

### 3. جواز السفر
* **صورة صفحة البيانات الكاملة:** `GET /api/passport/{id}/image`
* **الصورة الشخصية للوجه:** `GET /api/passport/{id}/photo`

---

## 6. جدول حالات الاستجابة والأخطاء (HTTP Status Codes)

| كود الحالة (Status) | المعنى | سبب الحدوث والإجراء المطلوب من تطبيق الموبايل |
| :---: | :--- | :--- |
| `200 OK` | نجاح العملية | تمت العملية بنجاح، وجميع البيانات المطلوبة موجودة بالـ Body |
| `400 Bad Request` | خطأ في المدخلات | نقص في الحقول الإلزامية أو صيغة التاريخ غير متوافقة |
| `401 Unauthorized` | غير مصرح | التوكن منتهي الصلاحية أو غير موجود؛ يجب توجيه المستخدم لتسجيل الدخول |
| `403 Forbidden` | صلاحيات غير كافية | المستخدم غير مصرح له بتعديل وثائق موظف آخر، أو الحساب غير مرتبط بموظف |
| `404 Not Found` | غير موجود | رقم الموظف المطلوب غير موجود في النظام |
| `423 Locked` | الحساب مغلق | تم قفل الحساب بسبب محاولات دخول خاطئة متعددة |
| `500 Server Error` | خطأ في الخادم | خطأ غير متوقع في قاعدة البيانات أو التخزين السحابي |
