# EmployeeProfile

The employee's own view of their record: a summary on top, every HR domain one tab away, and the things that need their action in a side rail.

Built on the pattern most enterprise HR suites converge on (a header with identity, KPI tiles, tabbed domains, a context rail). The consumer provides the employee record, the permissions of the viewer, and handlers for "تقديم طلب", "تحديث بياناتي", reveal and download.

## Structure, top to bottom

1. **Header card**: avatar (initial on `accent`), name in `h1`, title · section · department, then badges for status, employee ID (`hr-num`) and grade. Actions at the inline end: one primary ("تقديم طلب"), one secondary ("تحديث بياناتي"). No HR-admin actions here: this view is for the employee.
2. **KPI tiles** (4): leave balance, length of service, attendance rate, open requests. Each has a label, a `num-lg` value and a one-line footnote that says what the number is measured against.
3. **Tabs** in this order: نظرة عامة، البيانات الشخصية، الوظيفة، المؤهلات، الإجازات والدوام، الراتب، المستندات. Overview repeats the six fields people look up most, plus profile completion with a direct fix link.
4. **Side rail**: direct manager, "يحتاج انتباهك" (expiring documents, missing fields), recent activity.

## Field coverage (what "all the employee's information" means)

| Tab | Sections and fields |
| :--- | :--- |
| البيانات الشخصية | Full four-part name, English name, date of birth, gender, marital status, national ID (masked); personal phone and email, address, emergency contact |
| الوظيفة | Department, section, unit, job title, grade and step, shift; career timeline (appointment, transfers, promotions with order numbers) |
| المؤهلات | Degree, specialization, institution, year, grade, verification status; training courses with hours |
| الإجازات والدوام | Balances per leave type, recent requests with status |
| الراتب | Base salary and allowances (masked), bank account (last 4 only), payslips to download |
| المستندات | Appointment order, ID documents, certificates, each with validity status |

## Rules

- Sensitive values (national ID, salary, allowances) start masked with an "إظهار" button; every reveal is audit-logged server-side, and the Payroll heading says so.
- Missing data shows a `warn` badge ("غير مُدخلة") with a way to fix it, never a blank or a dash.
- Dates as `YYYY/MM/DD` in `hr-num`; phone numbers `dir="ltr"`.
- Tabs follow the ARIA tabs pattern: arrow keys move, Home/End jump, only the selected tab is in the tab order.

## Screens

The component responds to its own width (container queries), so it works the same inside a sidebar layout or full-screen.

| Container width | Layout |
| :--- | :--- |
| under 480px (phones) | Header stacks; action buttons share one full-width row; KPIs 2×2; fields one column; tabs scroll sideways with snap; rail drops below the tabs |
| 480–767px | Avatar 80px, actions move to the header's end; fields two columns |
| 768–1023px (tablets) | KPIs in one row of four; overview fields three columns |
| 1024px and up | Side rail (320px) beside the tabs |
