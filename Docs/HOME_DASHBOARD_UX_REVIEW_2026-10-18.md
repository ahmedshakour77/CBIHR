# Home dashboard UX review

**Implementation update, 19 October 2026:** all twelve findings below have been addressed in source. See [the fix and verification record](HOME_DASHBOARD_UX_FIXES_2026-10-19.md). The original observations are retained as the review baseline.

Date: **18 October 2026**  
Page: `/`  
Scope: **user experience only**. This report covers the home dashboard and the immediate interactions it opens. It does not assess backend security, database correctness, or release readiness.

## Main assessment

The dashboard has a clear Arabic RTL identity, useful workforce indicators, and several good accessibility foundations. Its main weakness is the journey from seeing a number to completing a task. Some actions change content outside the viewport, report browsing stops at 150 records, and employee actions lead back to the directory instead of directly to the person.

The first screen also gives similar emphasis to seven filters, four header actions, and twelve KPI cards. On a phone, users encounter almost a full screen of controls before the first result.

**Recommended first pass:** repair filter behavior and report destinations, make shortcut results visible, and simplify the phone's opening screen. Preserve the current visual identity.

## What I checked

- Opened the running dashboard in Edge in the existing signed-in session.
- Inspected the desktop interface at approximately **1912 × 948 CSS px**.
- Inspected the phone layout at **390 × 844 CSS px**.
- Opened the missing-photo report and checked its result count, rendered rows, and navigation controls.
- Tested keyboard containment in the report dialog with Shift+Tab. Focus stayed inside the dialog.
- Selected both photo-presence options in the digital-status filter, inspected the resulting empty state, and cleared the selections.
- Clicked the retirement shortcut and measured the location of its result section.
- Cross-checked the UI against `Home.razor`, the shared filter/dialog components, directory routing, and relevant styles/scripts.
- Ran the bundled read-only implementation detector on `Home.razor`. It returned `[]`. Its clean result does not cover the interaction defects reproduced below.

The findings distinguish **observed behavior** from **source evidence**. Browser timing, screen-reader output, dark mode, other roles, print output, and downloaded CSV contents were not comprehensively tested. No employee records or application settings were changed. The temporary viewport, filters, and analytic section were restored after inspection.

## Priorities

There are **12 findings: 6 P1 major and 6 P2 minor**. No P0 task blocker was established in this review.

| Priority | Finding | Evidence |
|---|---|---|
| P1 | Opposite digital-status selections produce an unexplained zero result | Reproduced live |
| P1 | Retirement shortcut changes content far below the viewport | Reproduced live |
| P1 | “View employee file” targets the employee list | Live link and source |
| P1 | Reports stop at 150 rows without in-app pagination | Reproduced live |
| P1 | Photo-gallery shortcut targets an undeclared route | Source-confirmed route mismatch |
| P1 | Phone users reach KPIs only after the large control stack | Measured live |
| P2 | Empty/loading states can imply missing source data | Observed and source-confirmed |
| P2 | KPI labels and meters need clearer definitions | Live content and source |
| P2 | Filter scope and freshness do not travel clearly with results | Live layout and print source |
| P2 | Export failure has no user-facing recovery state | Source evidence |
| P2 | Quick-report count and interaction promise disagree with the controls | Counted live |
| P2 | Phone filter triggers are only 40 px high | Measured live |

## Detailed notes

### UX-01 · P1 · Opposite filter choices unexpectedly remove the whole workforce

**Observed:** selecting both `صورة شخصية موثقة` and `بدون صورة شخصية` resulted in a total workforce of **0**, although the unfiltered dashboard contained records in both categories. The filter allows both choices and offers `تحديد الكل`.

**User impact:** a familiar multi-select pattern suggests “include these categories.” Here, selecting more categories can remove every result without explaining the contradiction. Users may conclude that records are missing.

**Recommendation:** split the control into clear dimensions: photo status, contact completeness, and linked-account status. Use a single choice within mutually exclusive dimensions, or define union behavior within a dimension and intersection across dimensions. Explain the combination rule beside the controls. Prevent `تحديد الكل` from creating an impossible state.

**Acceptance check:** selecting all photo categories preserves the full photo-status population. An impossible combination is prevented or explained before presenting an unexplained zero.

**Source:** [Home.razor](../API.HR.Node/Components/Pages/Home.razor#L3636), digital predicates in `BuildFilteredEmployeeQueryAsync` and the main dashboard query; [MultiSelectDropdown.razor](../API.HR.Node/Components/Common/MultiSelectDropdown.razor), `SelectAll` and `ToggleOption`.

### UX-02 · P1 · A quick report can change a section users cannot see

**Observed:** clicking `توقعات التقاعد (10 سنوات)` selected the retirement analytic section, but the analytic heading remained approximately **2,233 px below the viewport top** in a **948 px high** viewport. The quick-report bar remained visible. No dialog opened.

**User impact:** the click appears to do nothing. Users must discover that they need to scroll past other charts to find the result. The surrounding instruction promises that clicking a report will show a list of names, but this button changes a lower section instead.

**Recommendation:** make the action reveal the destination and move keyboard focus to its heading. Alternatively, open the same report dialog pattern used by other quick reports. Give section-navigation actions a distinct label such as `فتح تحليل التقاعد`.

**Acceptance check:** one click produces visible, clearly identified results. Keyboard users arrive at the same result, and closing/back navigation preserves context.

**Source:** [Home.razor:604](../API.HR.Node/Components/Pages/Home.razor#L604), the generations shortcut at line 660, and `SwitchTab` at line 2651. The same pattern deserves checking on the treemap's detailed-report action.

### UX-03 · P1 · The employee-file action leads to the directory

**Observed/source:** report rows label their action `عرض ملف الموظف في السجلات`, but the link targets `Employees?search=<employee number>` in a new tab. It does not target the employee's profile route. The reviewed directory initializes its own search state and contains no query-string binding for that `search` parameter.

**User impact:** users who have already identified a person must search or select again. The wording promises a more direct destination than the link provides. This is especially costly during repeated photo-maintenance work.

**Recommendation:** link directly to the employee profile and the relevant section when available. For a missing-photo report, offer `رفع صورة` or `فتح الملف` with a clear destination. If the directory remains the destination, support the query parameter and label it `فتح الموظف في الدليل`.

**Acceptance check:** opening any report row lands on that employee's record, or on a directory already filtered to that person. No second search is required.

**Source:** [Home.razor:2150](../API.HR.Node/Components/Pages/Home.razor#L2150); [EmployeeDirectory.razor](../API.HR.Node/Components/Pages/EmployeeDirectory.razor#L1478), search state and initialization. The destination was inspected in source, not followed through a live employee-editing flow.

### UX-04 · P1 · Reports require export to browse beyond 150 records

**Observed:** the missing-photo dialog showed **391 results**, rendered **150 rows**, and provided no pagination or “load more” control. Its message directed the user to export the complete list.

**User impact:** users can inspect an exception list but cannot work through the whole queue inside the application. Search helps locate a known employee, but it does not replace browsing the remaining records. Export adds a context switch to a task that should support repeated review and correction.

**Recommendation:** add pagination or incremental loading with a visible range, for example `1–50 من 391`. Preserve search and page position when users return from an employee record. Keep CSV as an optional output.

**Acceptance check:** users can reach result 151 and the final result without downloading a file, then return to the same position after opening a record.

**Source:** [Home.razor:2129](../API.HR.Node/Components/Pages/Home.razor#L2129), `Take(150)` and the explanatory message at line 2160.

### UX-05 · P1 · The photo-gallery shortcut has a route mismatch

**Source evidence:** the photo KPI's gallery action points to `employee-gallery`. The gallery component declares `/gallery`, and no `/employee-gallery` page route was found. The sidebar uses `/gallery`.

**User impact:** two controls promising the same gallery lead to different addresses. The KPI shortcut is likely to create a dead end instead of helping users resolve incomplete photos.

**Recommendation:** use the same verified gallery route everywhere. Ideally carry the relevant photo filter into the destination.

**Acceptance check:** the KPI gallery shortcut and sidebar gallery link both open the intended gallery. Back navigation restores dashboard filters.

**Source:** [Home.razor:327](../API.HR.Node/Components/Pages/Home.razor#L327); [EmployeeGallery.razor:1](../API.HR.Node/Components/Pages/EmployeeGallery.razor#L1).

**Verification limit:** a direct browser check of `/employee-gallery` was blocked by the client, so this review does not claim a reproduced HTTP 404.

### UX-06 · P1 · The phone's opening screen prioritizes controls over results

**Observed:** at **390 × 844 px**, seven full-width filters and four header actions appeared before the KPIs. The first KPI began around **736 px** from the viewport top. Twelve KPI cards then formed a long single-column sequence. Desktop also presented all twelve with similar visual weight.

**User impact:** users must scroll before learning the workforce's current state. Frequent tasks such as checking missing photos or today's absence compete with less frequent actions such as importing data and launching the display screen.

**Recommendation:** lead with a small summary and the most important exception actions. Keep one or two common filters visible and place the rest behind an explicitly labeled `تصفية` control with an active count. Group import/print/display actions by purpose. Retain every function through progressive disclosure.

**Suggested initial structure:** title and scope, key exceptions, four primary KPIs, quick reports, then detailed analytics. Treat the precise KPI selection as a product decision to validate with HR staff.

**Acceptance check:** a phone user can see a meaningful KPI and an exception action in the initial viewport. Opening advanced filters reveals all existing criteria and their active selections.

**Source:** [Home.razor:29](../API.HR.Node/Components/Pages/Home.razor#L29), header controls and the twelve-card KPI section.

### UX-07 · P2 · Empty states confuse “no matches” with “no data exists”

**Observed:** with the contradictory photo filter active, the grades area said `لم تُسجل درجات وظيفية بعد.` This describes absent master data, although the current selection was the cause. During initial loading, quick-report counts and some analytic empty messages were also present before the statistics settled.

**User impact:** users cannot confidently distinguish loading, an empty selection, and missing organizational data.

**Recommendation:** give loading, no matches, no records, and failure distinct states. For a filtered empty result, use `لا يوجد موظفون يطابقون التصفية الحالية` and offer `مسح التصفية`. Render pending counts as a placeholder rather than a factual zero. Announce result changes without moving focus unnecessarily.

**Acceptance check:** a known filter with no matches displays a filter-specific explanation and recovery action. Pending data never presents an authoritative zero count or “not registered” conclusion.

**Source:** [Home.razor:1015](../API.HR.Node/Components/Pages/Home.razor#L1015), grade empty state; quick-report counts from line 598.

### UX-08 · P2 · KPI labels and meters need explicit definitions

**Observed/source:** `متوسط سنوات الخدمة` shows an average, while the meter beneath it represents the proportion with over 20 years of service. The training card shows a record count while its meter represents employee coverage. `الصور الشخصية الموثّقة` counts employees with active photos, and `حسابات البوابة المفعّلة` counts linked user accounts. Their labels imply verification or activation beyond what those counting expressions establish.

**User impact:** readers may interpret the meter as a scale of the headline number or infer a stronger operational status than the metric actually represents.

**Recommendation:** give each KPI a plain definition and visible denominator. Label meters with the measure they depict. Use wording that matches the recorded state, such as `موظفون لديهم صورة` and `حسابات مرتبطة بموظفين`, unless an additional verification/activation definition exists. Give the digital-completeness card a direct action to see incomplete records.

**Acceptance check:** a new HR user can explain each headline, meter, and denominator without opening source code or guessing from color.

**Source:** [Home.razor:326](../API.HR.Node/Components/Pages/Home.razor#L326), linked accounts at line 352, service at line 376, training at line 486, and metric calculations at lines 3125–3139.

### UX-09 · P2 · Scope and freshness are easy to lose when reading or printing

**Observed/source:** active selections appear near the top, while the update time appears much farther down beside the analytic explorer. The time shows only `HH:mm:ss`. Print styles hide `.hr-alert`, which includes the active-filter summary.

**User impact:** a reader can lose track of which workforce the figures describe. A printed report may omit the active-filter summary that made the numbers interpretable on screen.

**Recommendation:** place a compact scope/result-count/updated-at line beside the opening KPIs. Repeat that context in report dialogs and exported/printed report headers, including the date. Preserve active filters in print as report metadata, even when interactive filter controls are hidden.

**Acceptance check:** a printed filtered dashboard states the selected scope, date/time, and matching workforce count. Opening a drill-down makes the inherited scope clear.

**Source:** [Home.razor:160](../API.HR.Node/Components/Pages/Home.razor#L160), active filters; update time at line 948; print styles at line 2172. Print output was reviewed in source, not rendered in a print preview.

### UX-10 · P2 · CSV failure has no visible recovery message

**Source evidence:** `ExportModalReportCsv` catches an exception and logs it without setting a user-facing error or retry state. The reviewed export button also has no pending/success state.

**User impact:** if export fails, users cannot distinguish an ignored click from a completed download. Repeated clicking becomes the only apparent recovery.

**Recommendation:** show a short pending state and an actionable error such as `تعذّر تنزيل التقرير. أعد المحاولة.` Keep the dialog and search state intact. Confirm export scope, especially when users have searched within a larger report.

**Acceptance check:** a simulated download failure shows a recoverable message. The button prevents duplicate starts while preparing the file, and the label makes clear whether it exports the filtered results.

**Source:** [Home.razor:2098](../API.HR.Node/Components/Pages/Home.razor#L2098) and [ExportModalReportCsv](../API.HR.Node/Components/Pages/Home.razor#L4222). No export failure was induced in the running system.

### UX-11 · P2 · The quick-report count and promise do not match its contents

**Observed:** the badge says `9 تقارير فورية نشطة`. The group contains **10 report buttons plus a Canva link**. Some controls open dialogs, some change a lower analytic section, and Canva navigates to another page. The shared introduction says clicking any report shows employee lists and export.

**User impact:** users cannot predict a control's outcome from the group's common presentation. The stale count reduces confidence in the dashboard's other labels.

**Recommendation:** derive any report count from the visible set, or remove it. Separate employee lists, analytic sections, and report-generation destinations with clear labels and consistent behavior.

**Acceptance check:** the visible count matches its definition, and each control's label makes the destination or result clear before clicking.

**Source:** [Home.razor:585](../API.HR.Node/Components/Pages/Home.razor#L585), report introduction, badge, and controls.

### UX-12 · P2 · Phone filter targets leave limited room for touch

**Observed:** each of the seven filter trigger buttons measured **40 CSS px high** at the phone viewport. They are frequently used controls in a tightly repeated stack.

**User impact:** precise selection takes more effort on a phone, particularly while operating one-handed or using larger text settings.

**Recommendation:** use a comfortable touch target of at least 44 px for these controls and verify spacing with the application's density/font settings. This is a usability recommendation, not a claim of a measured WCAG failure.

**Acceptance check:** every filter remains easy to target at phone widths, including compact density and large text, without introducing horizontal page overflow.

**Source:** [MultiSelectDropdown.razor](../API.HR.Node/Components/Common/MultiSelectDropdown.razor) and `.hr-ms-trigger` in [hr-ui.css](../API.HR.Node/wwwroot/hr-ui.css#L2337).

## What works well

- **Arabic RTL reading order:** titles, numbers, filter wording, and the desktop four-column KPI layout form a consistent visual language. Preserve this foundation.
- **Filter visibility:** selected filters become removable chips, and the clear-all action is easy to understand. Reuse this pattern after repairing contradictory selections.
- **Real drill-down value:** missing photos and other workforce exceptions open useful employee lists with search and result totals.
- **Dialog keyboard support:** the report dialog received focus on open, and the tested Shift+Tab interaction stayed inside it. Shared dialog scripts implement focus cycling and return-to-trigger behavior. Do not replace this with a less accessible overlay.
- **Accessible control naming:** photo-report actions, many chart actions, and shared filter triggers have descriptive names. The heatmap retains a table representation.
- **Responsive reflow:** the dashboard changed to a single-column phone layout, and no horizontal page overflow was found in the inspected opening state. The issue is prioritization and length, not a completely broken mobile layout.
- **Loading and retry foundations:** KPI skeletons and a clear dashboard retry action already exist. Extend that distinction to the other result sections.

## Suggested UX change sequence

1. **Predictable actions:** repair digital-filter combinations, employee/gallery destinations, and quick-report reveal/focus behavior.
2. **Complete report browsing:** add pagination, direct employee actions, and preserved return position.
3. **Opening-screen hierarchy:** reduce the permanent filter/action stack and choose a small primary KPI set for phone users.
4. **Trust and feedback:** clarify KPI definitions, show scope/freshness beside results, distinguish empty/loading states, and surface export failures.
5. **Consistency pass:** correct report counts, standardize touch targets, and check Arabic labels across the full journey.

The corresponding impeccable commands would be `harden` for interaction and recovery, `adapt` for the phone layout, `clarify` for metric/action wording, then `polish` after the behavior is correct. This report proposes changes; it does not implement them.

## Follow-up acceptance session

Use the same desktop and phone sizes for a short task-based check:

1. Identify the workforce scope and last update from the opening screen.
2. Find employees missing photos and reach a record beyond result 150 without exporting.
3. Open that person's profile, then return to the same report/search position.
4. Select photo-status categories without creating an unexplained empty workforce.
5. Open retirement analysis with both pointer and keyboard and arrive at its visible heading.
6. Recover from an empty selection and a failed export without losing context.
7. Print a filtered report and confirm it retains scope and update metadata.

## Requested presentation template

The selected **Market Trends Report** reference contains six slides. Its metadata and the installed presentation skill were readable, and the reference was left unchanged. The local artifact authoring package was unavailable, and the supplied renderer failed because its runtime module directory was not configured. No local PowerPoint or LibreOffice renderer was found in the checked locations.

Consequently, this delivery contains the completed Markdown UX review. A rendered, verified presentation could not be produced in this environment. No replacement runtime was installed and no unverified deck is presented as complete.
