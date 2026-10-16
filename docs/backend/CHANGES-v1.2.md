# Public Data APIs — Version 1.2
Subtitle: The services from the public API list (public-apis, Sports & Fitness) added to the backend plan: geocoding, weather, public holidays and local phone / email validation.
Document: Change log for INTEGRATIONS, PRD, ERD, API and STRUCTURE
Version: 1.2
Date: 17 October 2026
Owner: Sharaf (CEO)
Status: Applied — the five documents and their PDFs are updated
Audience: Founders, backend engineers

# 1. Summary

<div dir="rtl" class="ar-box" markdown="1">

**الملخص بالعربي:**

- من لينك الـ public-apis اللي بعته يوم 14 أكتوبر، كان **API-FOOTBALL** بس هو اللي متغطي بالكامل في الخطة. الخدمات التانية اللي وافقت عليها اتضافت دلوقتي:
    1. **تحويل العنوان لدبوس على الخريطة والعكس (Nominatim):** في شاشة إضافة الملعب، وطلب صاحب الملعب، وعناوين المصنع والبائع. البحث عن "ملاعب جنبي" مش بيكلم أي خدمة بره، بيتحسب عندنا في قاعدة البيانات.
    2. **الطقس (Open-Meteo):** توقعات الساعة المحجوزة في صفحة الملعب ووقت الحجز للملاعب المكشوفة، وتنبيه المطر أو الحر في تذكير اليوم اللي قبل الماتش، وتقويم التسويق عند البائعين. **مهم:** النسخة المجانية لغير الاستخدام التجاري، فلازم نشترك في الخطة المدفوعة قبل الإطلاق.
    3. **الأجازات الرسمية (Nager.Date أو Calendarific):** جدول جديد في قاعدة البيانات اسمه `ops.PublicHolidays`. أجازات العيد بتتحفظ "متوقعة" والأدمن يأكدها بعد الإعلان. وبتستخدم في اقتراح أسعار الأجازات لأصحاب الملاعب، وتوقع الإيراد، وتقويم البائعين.
    4. **التحقق من الموبايل والإيميل:** بمكتبة جوه السيرفر من غير أي خدمة بره، فمفيش تكلفة ومفيش بيانات شخصية بتطلع من عندنا.
- واتكتب في الخطة كمان اللي **اترفض** من نفس اللينك وسببه: المراهنات، والعملات، والـ QR، والرياضات التانية.

</div>

| # | Service | Provider | Status in the plan | Priority |
|---|---|---|---|---|
| 1 | Football data | API-FOOTBALL | already covered (INTEGRATIONS §7) | Must |
| 2 | **Geocoding** (address ↔ pin) | Nominatim now; Geoapify / LocationIQ later | **added** | Must |
| 3 | **Weather** | Open-Meteo (commercial plan before launch) | **added** | Should / Later |
| 4 | **Public holidays** | Nager.Date (Egypt coverage to verify) or Calendarific | **added** + new table | Should |
| 5 | **Phone & email validation** | local library `libphonenumber-csharp` + confirmation email | **added** | Must |
| — | Betting, currency, QR APIs, other sports, highlight videos | — | **listed as not used**, with the reason | — |

# 2. Changes per document

## 2.1 INTEGRATIONS
| Where | Change |
|---|---|
| §2 Integration map | 4 new rows: Geocoding, Weather, Public holidays, Phone & email validation — interface, provider, systems that use it, responsible human and AI agent |
| §9.2 Maps and geocoding | Rewritten: "near me" uses SQL distances (no outside call); `IGeocoder` with search and reverse; **Nominatim usage policy** (1 request/second, identifying user agent, attribution, no bulk, server cache); later paid geocoder and the questions to ask it |
| §9.5 Weather (new) | `IWeatherProvider`; uses (pitch page and checkout, day-before alert, seller calendar, partner insights); cached per area every 3 hours; **free API is non-commercial → commercial plan before launch** |
| §9.6 Public holidays (new) | `IHolidaysProvider`; stored in `ops.PublicHolidays`; Nager.Date (verify Egypt) or Calendarific; Islamic holidays saved as estimated and confirmed by an admin; uses in insights, forecast, calendar, what-if |
| §9.7 Phone and email validation (new) | libphonenumber-csharp to `+20…`; email proven by the confirmation link; optional disposable-domain list; no paid API |
| §9.8 Not used (new) | Betting/odds, currency, QR, other sports, highlight videos — with reasons |
| §10 Secrets | New variables: `GEO__PROVIDER`, `GEO__USER_AGENT`, `GEO__APIKEY`, `WEATHER__OPENMETEO__APIKEY`, `HOLIDAYS__PROVIDER`, `HOLIDAYS__CALENDARIFIC__APIKEY` |
| §12 Responsibility matrix | 3 new rows: geocoding, weather plan, holidays sync and confirmations |

## 2.2 PRD
**New section 8.5 "Public data services":**

| ID | Requirement | Priority |
|---|---|---|
| FR-GEO-01 | Address ↔ map pin in the pitch editor, the pitch-owner application, factory and seller addresses | Must |
| FR-GEO-02 | "Near me" from browser position + SQL distances, no outside call per search | Must |
| FR-WX-01 | Forecast on the pitch page and at checkout (outdoor pitches, up to 7 days) | Should |
| FR-WX-02 | Rain / heat alert in the day-before reminder | Later |
| FR-WX-03 | Real forecasts in the seller marketing calendar (Rising and up) | Should |
| FR-WX-04 | Open-Meteo commercial plan before launch | Must (before launch) |
| FR-HOL-01 | Egyptian holidays in `ops.PublicHolidays`, Islamic dates estimated then confirmed | Should |
| FR-HOL-02 | Holidays in partner insights, seller calendar, CEO what-if, availability badge | Should |
| FR-VAL-01 | Local phone and email validation, no outside API | Must |

Plus the list of services from the same public list that are not used.

## 2.3 ERD
| Where | Change |
|---|---|
| Creation order, Part A | New **A25 `ops.PublicHolidays`** (no foreign keys) |
| A25 (new table) | `Country` (DF `'EG'`), `Date`, `NameEn` / `NameAr`, `Kind` (National, Religious, Observance), `IsDayOff`, `IsEstimated`, `ConfirmedById`, `ConfirmedAt`, `Source` (nagerdate, calendarific, admin); UQ (Country, Date, NameEn); index on Date |
| A8 `ops.Integrations.Category` | CHECK widened from 0–8 to **0–11**: Geocoding, Weather, Holidays (keys `geo.nominatim`, `weather.openmeteo`, `holidays.nagerdate`) |
| Totals | **25 lookup + 124 child + 13 junction = 162 tables** (was 161) |
| Seed step 10 | Public holidays of the current and next year from the first holidays sync |
| SSMS checks | 162 tables |

## 2.4 API
| Where | Change |
|---|---|
| §2.4 Lookups | **`GET /lookups/holidays?year`** — public holidays, estimated dates flagged |
| §3.1 Pitches | **`GET /pitches/{slug}/weather?date`** — hourly forecast for outdoor pitches (cached 3 h; 204 for indoor or beyond 7 days); availability grid gets a holiday badge |
| §3.2 Partner | **`GET /geo/search?q`** and **`/geo/reverse?lat&lng`** — Egypt only, 1 request/second per account, cached; 422 outside Egypt, 503 when the provider is busy |
| §7.1 Permission catalogue | New permission **`content.holidays`** |
| §7.2 Admin | **`/admin/holidays`** — confirm or move estimated Islamic dates, add a holiday, re-sync |

## 2.5 STRUCTURE
| Where | Change |
|---|---|
| §1.2 Tree | `Infrastructure/Integrations/Geo` (NominatimGeocoder), `Weather` (OpenMeteoClient), `Holidays` (NagerDateClient, CalendarificClient) |
| §4 Packages | **`libphonenumber-csharp`** — local phone validation |

# 3. To confirm before relying on them
1. **Nager.Date:** that it returns Egypt's public holidays; otherwise use Calendarific.
2. **Open-Meteo:** the current price of the commercial plan, to be taken before launch.
3. **Nominatim:** volume stays low (partners adding venues); if address search grows, move to a paid geocoder.
