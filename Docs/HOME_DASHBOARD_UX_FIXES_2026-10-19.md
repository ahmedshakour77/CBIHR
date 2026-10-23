# Home dashboard UX fixes

Completed in source: **19 October 2026**. Scope: the twelve findings in [the dashboard UX review](HOME_DASHBOARD_UX_REVIEW_2026-10-18.md).

The changes preserve the Arabic RTL design, existing dashboard capabilities, and shared dialog accessibility. They add clearer task flows and progressive disclosure to the home page.

## Changes by finding

| Finding | Change | Verification |
|---|---|---|
| UX-01: contradictory digital selections | Replaced the combined digital multi-select with separate single-choice controls for photo status, contact completeness, and linked accounts. Overview and drill-downs use the same query helper, which also safely handles legacy opposite selections as a union within their category. | Five automated filter regressions passed, including SQL translation. Browser checks confirmed 391 missing-photo results, 3 complete-contact results, and recovery from a genuinely empty combination. |
| UX-02: shortcut results outside the viewport | Analytic shortcuts now reveal their destination and move keyboard focus to its heading. The behavior respects reduced-motion preferences. Labels distinguish opening analysis from opening an employee list. | Browser check confirmed the retirement heading received focus and appeared at approximately 96 px from the viewport top. |
| UX-03: employee-file action opens directory | Report rows link directly to `employees/{employee number}` in a new tab. Accessible wording explains that the report remains in its original position. | Verified rendered link targets and route declarations. The report's current search/page remains in its original tab. |
| UX-04: 150-row browsing cap | Replaced truncation with 50-row pagination, result ranges, page counts, and previous/next controls. Search resets pagination, and page changes move focus to the results heading. | Browser check reached results 151–200 of 391 on page 4 of 8. Search-empty recovery and clear-search were also checked. |
| UX-05: gallery route mismatch | Changed the KPI shortcut to the declared `gallery` route. Added scoped dashboard view state for retaining filters, expanded indicators, and analytic selection across navigation within the user's Blazor circuit. | Route and state handling verified in source. The final gallery/back browser confirmation was interrupted by the browser session failure. |
| UX-06: controls overwhelm the phone opening screen | Kept formation/status filters visible, placed remaining criteria behind “تصفية متقدمة”, and grouped print/import/display actions under “طباعة وأدوات”. The first four indicators are workforce, photos, today's leave, and retirement horizon. All eight remaining indicators are available under “عرض بقية المؤشرات”. | Desktop and 390 × 844 phone screenshots inspected. First phone KPI moved from approximately 736 px to 493 px; the missing-photo action was also inside the initial viewport. |
| UX-07: misleading loading/empty states | Pending data no longer renders the detailed report sections as factual zeros. A filtered zero result gets a specific explanation and clear-filter action. Search-empty results have a clear-search action. Failed loads retain filter choices and provide retry wording. | Browser checks confirmed the correct filtered-empty message and absence of the misleading “no grades registered” message. |
| UX-08: ambiguous KPI definitions | Renamed photo/account metrics to describe recorded presence/linkage. Clarified the image/phone/email completeness definition and percentage denominators. Added a list of incomplete digital files. Scoped appreciation-letter and course queries to the same selected workforce. | Source and rendered labels inspected. Existing suite and query tests passed. |
| UX-09: lost scope/freshness context | Added scope and successful update date/time beside the opening results and inside report dialogs. Kept the scope in print output and added scope, time, search, and result count to CSV exports. Date/time uses explicit text-direction isolation. | Dialog context checked in-browser. Print behavior checked in source. Automated export test confirms scope and the complete result set in the generated CSV. |
| UX-10: silent export failure | Added pending, success, and recoverable failure messages; disabled duplicate in-progress exports; preserved search/page on failure. Replaced dynamic JavaScript evaluation with a named Blob download helper. | Three new export tests passed: failure recovery preserves context, all search results export across pages, and an in-progress export cannot start a duplicate. Browser download-event confirmation did not complete. |
| UX-11: stale quick-report count/promise | Removed the hard-coded report count. Updated the introduction and navigation labels to distinguish employee lists, analytic destinations, and report-generation links. | Source scan confirmed the stale badge was removed; desktop content inspected. |
| UX-12: small phone filter targets | Added a minimum 44 px height to dashboard filter triggers/selects and report buttons, with responsive layouts for the additional criteria. | Browser measurements confirmed 44 px filter triggers. No horizontal overflow was found in the inspected phone opening state. |

## Files changed

- [Home.razor](../API.HR.Node/Components/Pages/Home.razor): dashboard layout, controls, scope, loading/empty states, pagination, destinations, and export feedback.
- [Home.razor.css](../API.HR.Node/Components/Pages/Home.razor.css): page-scoped responsive layout, progressive disclosure, touch sizing, and print rules.
- [DashboardDigitalFilter.cs](../API.HR.Node/Services/Employees/DashboardDigitalFilter.cs): common digital-filter behavior for the overview and reports.
- [HomeDashboardViewState.cs](../API.HR.Node/Services/Employees/HomeDashboardViewState.cs): dashboard choices retained within the user's circuit.
- [home-dashboard.js](../API.HR.Node/wwwroot/js/home-dashboard.js): focus/scroll behavior and CSV Blob download.
- [App.razor](../API.HR.Node/Components/App.razor): loads the fingerprinted dashboard helper.
- [Program.cs](../API.HR.Node/Program.cs): registers the scoped dashboard view state.
- [DashboardDigitalFilterTests.cs](../API.HR.Node.Tests/DashboardDigitalFilterTests.cs) and [HomeDashboardExportTests.cs](../API.HR.Node.Tests/HomeDashboardExportTests.cs): eight new regression cases.

## Automated verification

The final tested source passed **132 tests**, with **0 failed and 0 skipped**. This includes the eight new cases above.

The normal Release executable was locked by the already-running application. Verification therefore used a separate output directory:

```powershell
dotnet test API.HR.Node.Tests/API.HR.Node.Tests.csproj --no-restore --configuration Release -p:UseAppHost=false -p:OutputPath=C:/Users/Administrator/AppData/Local/Temp/2/home-ux-verification/ --logger 'console;verbosity=minimal'
```

Additional checks:

- `node --check API.HR.Node/wwwroot/js/home-dashboard.js` passed.
- The impeccable implementation detector returned `[]` for `Home.razor`.
- Source checks found no remaining `Take(150)`, `employee-gallery`, `Employees?search=`, or stale nine-report badge in the dashboard.
- Both overview and drill-down code paths call the same digital-filter helper.

The build still reports existing warnings for obsolete FIDO configuration names, a nullable dictionary key in `AdminLookupsPage`, and Windows-specific Active Directory usage. These are outside this UX change.

## Browser verification and limits

A temporary HTTPS preview used port 7279 and the tested build, with startup migrations disabled. The original application process was left untouched. Desktop and phone layouts and the principal report/filter flows were inspected in a bounded verification pass, followed by confirmation checks.

The final browser download-event check did not complete and reset the browser automation session. Therefore a completed browser download and its saved file are **not** claimed as verified. Export payload completeness, failure feedback, preserved context, and duplicate prevention are covered by the passing automated tests.

A physical print/PDF export, full screen-reader session, all theme/density combinations, and every employee-detail destination were not tested. Print metadata rules and destination construction were checked in source. Returning across navigation retains dashboard choices within a live Blazor circuit; a full browser reload or new session starts a new circuit.

Rebuild and restart the application's normal host to load the new assembly and fingerprinted assets. The temporary verification build is not a production deployment.
