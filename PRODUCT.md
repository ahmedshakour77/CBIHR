# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Arabic-speaking HR administrators of a government/enterprise HR system ("مسؤول النظام"). They work in an authenticated back office, in Arabic, right-to-left. Confirmed jobs they open the employee roster to do, all four equally weighted:

- photo maintenance at scale — find employees missing a profile photo and upload/replace photos in bulk sessions;
- look up one employee — search by name or employee ID and read their record;
- scan the workforce — filter by formation, job title, and status to understand who is where;
- use the roster as a hub — jump to contacts, gallery, or a record's other actions.

Usage scene confirmed as **all screens** for the back-office roster: phone through wide desktop monitors, every breakpoint a first-class layout.

A second, distinct audience exists for the workforce statistics surface: **nobody operates it**. It runs unattended on a wall display or TV in the office, read at 2–4 m by staff and visitors passing through. There is no pointer, no keyboard, and no session. Confirmed by the user on 2026-08-30.

## Product Purpose

API.HR.Node is an ASP.NET Core (.NET) HR system with a Blazor Server back office plus a REST API (Swagger). It holds the employee master record and manages employee profile photos in private server storage, with versioning, auditing, and authorization on every photo read and write.

## Operating Context

- Blazor Server (`@rendermode InteractiveServer`) pages under `API.HR.Node/Components/Pages`, wrapped by `Components/Layout/MainLayout.razor` (sticky top bar) and `Components/Layout/NavMenu.razor` (taskbar navigation).
- Existing surfaces: Home dashboard (`/`), Employees (`/Employees`), Employee Gallery (`/gallery`), Employee Contacts (`/employee-contacts`), Users (`/users`), Audit Logs (`/audit-logs`), Auth (`/Account/Login`).
- Whole app is `dir="rtl"`, Arabic UI copy, English/Latin only for technical labels (Swagger, Private Storage) and numerals.
- Theming is handled client-side via `[data-theme="light"|"dark"]` on `document.documentElement`, controlled by `wwwroot/js/hr-prefs.js` (`window.hrPrefs`, with `window.hrTheme.toggle()` kept as a shortcut) and synced via `localStorage('hr-prefs')` and the `hr-*` cookies the server reads before first paint. All styling resolves through `wwwroot/hr-ui.css` CSS design tokens.
- Data access from pages is direct EF Core via `IDbContextFactory<ApplicationDbContext>` / `ApplicationDbContext`, with audit logging.
- Photos are served through `IEmployeeProfilePhotoService` and accessed via high-performance REST endpoints (`/api/employees/{id}/photo/thumbnail` and `/api/employees/{id}/photo`) with conditional HTTP ETag caching (304 Not Modified) to eliminate SignalR overhead and base64 bloat.

## Capabilities and Constraints

- Employee record fields available to the roster (`EmployeeResponse`): Formation, EmployeeID, Name, JobTitle, PositionTitle, Stage, Grade, EducationLevel, Specialization, LastJobTitleDate, HireDate, Gender, Religion, Nationality, FirstDepartment, BirthDate, EmployeeStatus, HasProfilePhoto, ProfilePhotoVersion.
- Lookup tables for filters: `EmployeeStatuses`, `Formations`, `JobTitles`.
- Roster capabilities that must be preserved: text search (ID or name), photo-presence filter, formation filter, job-title filter, status filter, reset, card/table view modes, page size (12/24/48/96), pagination, per-employee photo upload/replace (jpg/jpeg/png/webp, 10 MB cap), full-size photo preview with download, photo delete, link to that employee's contacts page, and the KPI counts (total, with photo, without photo, completion rate).
- Front end uses a dedicated design token system (`wwwroot/hr-ui.css`), with IBM Plex Sans Arabic for Arabic typography, Inter for tabular numbers, and native RTL styling. Zero dependency on external CSS runtime frameworks.
- Test coverage includes automated unit tests (`API.HR.Node.Tests`).

## Brand Commitments

- Product name `API.HR.Node`, sub-label "Enterprise System"; brand mark `AML_Logo.png` / `logo.png`.
- Arabic-first RTL interface; IBM Plex Sans Arabic is the primary Arabic UI typeface loaded app-wide, with Inter for tabular figures.
- The surrounding shell (top bar, taskbar, other pages) is out of scope for this work and keeps its current identity.

## Evidence on Hand

- Real employee data in SQL Server behind `ApplicationDbContext`; real profile photos in `API.HR.Node/App_Data/Storage`.
- No marketing copy, testimonials, benchmarks, or pricing exist and none may be invented.

## Product Principles

1. The record is the product — every screen serves finding, reading, and correcting the employee master record.
2. Photo completeness is a first-class operational metric, not decoration; the interface should always make the gap visible and one click from being fixed.
3. Arabic RTL is the native reading direction, never a mirrored afterthought.
4. Every photo read and write is audited and authorized; the UI must never imply a shortcut around that.
5. The back office is used all day on every screen size — density serves scanning, and no capability may disappear on a phone.

## Accessibility & Inclusion

No formal standard established. Practical requirement: Arabic RTL correctness, readable at back-office viewing distance, and full keyboard operability of search, filters, and per-row actions.
