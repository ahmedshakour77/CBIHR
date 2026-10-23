using API.HR.Node.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace API.HR.Node.Services.Pdf;

public sealed class EmployeeDossierPdfDocument : IDocument
{
    private readonly EmployeeDossierPdfModel _m;

    // Institutional Color Palette (Central Bank of Iraq)
    private const string NavyPrimary = "#0f2942";
    private const string BlueAccent = "#1d4ed8";
    private const string GoldAccent = "#b45309";
    private const string SlateDark = "#1e293b";
    private const string SlateMuted = "#475569";
    private const string BorderColor = "#cbd5e1";
    private const string TableHeaderBg = "#f1f5f9";
    private const string CardBg = "#f8fafc";
    private const string RowAltBg = "#f8fafc";
    private const string RowWhite = "#ffffff";

    public EmployeeDossierPdfDocument(EmployeeDossierPdfModel model)
    {
        _m = model;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"خلاصة الخدمة والسجل الوظيفي - {_m.Employee.Name}",
        Author = "البنك المركزي العراقي - مديرية شؤون الموارد البشرية",
        Subject = "خلاصة الخدمة والسجل الوظيفي الإلكتروني المعتمد",
        Keywords = "HR, Employee, Dossier, Central Bank of Iraq, Verification",
        CreationDate = DateTime.Now
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24, Unit.Point);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily("Arial", "Segoe UI", "Tahoma", "Lato").FontSize(9f).FontColor(SlateDark));
            page.ContentFromRightToLeft();

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Right side: Official Republic / Bank header
                row.RelativeItem(4).Column(sub =>
                {
                    sub.Item().Text("جمهورية العراق").Bold().FontSize(12).FontColor(NavyPrimary);
                    sub.Item().Text("البنك المركزي العراقي").Bold().FontSize(11).FontColor(NavyPrimary);
                    sub.Item().Text("مديرية شؤون الموارد البشرية").FontSize(9).FontColor(SlateMuted);
                });

                // Center: Bank Logo
                if (_m.LogoBytes != null && _m.LogoBytes.Length > 0)
                {
                    row.RelativeItem(3).AlignCenter().AlignMiddle().Height(52).Image(_m.LogoBytes).FitHeight();
                }
                else
                {
                    row.RelativeItem(3).AlignCenter().AlignMiddle().Text("CBI HR").Bold().FontColor(NavyPrimary);
                }

                // Left side: Document reference and date
                row.RelativeItem(4).AlignLeft().Column(sub =>
                {
                    sub.Item().AlignLeft().Text(t =>
                    {
                        t.Span("الرقم الوظيفي: ").FontColor(SlateMuted).FontSize(8.5f);
                        t.Span(_m.Employee.EmployeeID).Bold().FontSize(10.5f).FontColor(BlueAccent);
                    });
                    sub.Item().AlignLeft().Text(t =>
                    {
                        t.Span("تاريخ الاستخراج: ").FontColor(SlateMuted).FontSize(8.5f);
                        t.Span(DateTime.Now.ToString("yyyy/MM/dd")).FontSize(8.5f);
                    });
                    sub.Item().AlignLeft().Text(t =>
                    {
                        t.Span("الحالة الوظيفية: ").FontColor(SlateMuted).FontSize(8.5f);
                        t.Span(_m.Employee.EmployeeStatus?.Name ?? "مستمر بالخدمة").Bold().FontSize(8.5f).FontColor(Colors.Green.Darken2);
                    });
                });
            });

            // Decorative separator line
            col.Item().PaddingTop(6).PaddingBottom(10).Column(sep =>
            {
                sep.Item().Height(2).Background(NavyPrimary);
                sep.Item().Height(1).Background(GoldAccent);
            });

            // Document Title Banner
            col.Item().PaddingBottom(8).Background(CardBg).Border(1).BorderColor(BorderColor).Padding(7).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("خلاصة الخدمة والسجل الوظيفي الإلكتروني المعتمد").Bold().FontSize(11.5f).FontColor(NavyPrimary);
                    c.Item().Text("مستند رسمي صادر إلكترونياً من منظومة إدارة الموارد البشرية الموحدة وموثق رقمياً").FontSize(7.5f).FontColor(SlateMuted);
                });
                r.AutoItem().AlignMiddle().Border(1).BorderColor(BorderColor).Background(Colors.White).PaddingHorizontal(8).PaddingVertical(3)
                    .Text("وثيقة رسمية معتمدة").Bold().FontSize(8).FontColor(BlueAccent);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            // Top Hero Profile Block (Enlarged Photo + Employee Identity + Prominent QR Code)
            col.Item().Element(ComposeHeroSection);

            col.Item().PaddingVertical(4);

            // Section 1: Personal & Family Data (No duplicate info)
            col.Item().Element(ComposePersonalInfoSection);

            col.Item().PaddingVertical(4);

            // Section 2: Civil ID & National Registry
            col.Item().Element(ComposeCivilIdentitySection);

            col.Item().PaddingVertical(4);

            // Section 3: Career Details & Service History
            col.Item().Element(ComposeCareerInfoSection);

            col.Item().PaddingVertical(4);

            // Section 4: Education & Specialization
            col.Item().Element(ComposeEducationInfoSection);

            col.Item().PaddingVertical(4);

            // Section 5: Contacts
            col.Item().Element(ComposeContactInfoSection);

            col.Item().PaddingVertical(4);

            // Section 6: Career Movements (if any)
            if (_m.CareerMovements.Count > 0)
            {
                col.Item().Element(ComposeCareerMovementsSection);
                col.Item().PaddingVertical(4);
            }

            // Section 7: Leaves Record (if any)
            if (_m.Leaves.Count > 0)
            {
                col.Item().Element(ComposeLeavesSection);
                col.Item().PaddingVertical(4);
            }

            // Section 8: Training Courses (with EndDate & Institution removed)
            if (_m.Courses.Count > 0)
            {
                col.Item().Element(ComposeCoursesSection);
                col.Item().PaddingVertical(4);
            }

            // Section 9: Appreciation Letters (if any)
            if (_m.AppreciationLetters.Count > 0)
            {
                col.Item().Element(ComposeAppreciationLettersSection);
                col.Item().PaddingVertical(4);
            }

            // Section 10: Disciplinary Penalties (if any)
            if (_m.Penalties.Count > 0)
            {
                col.Item().Element(ComposePenaltiesSection);
                col.Item().PaddingVertical(4);
            }

            // Section 11: Martyrs (ذوي الشهداء) (if any)
            if (_m.Martyrs.Count > 0)
            {
                col.Item().Element(ComposeMartyrsSection);
                col.Item().PaddingVertical(4);
            }

            // Section 12: Residences & Housing (معلومات السكن) (if any)
            if (_m.Residences.Count > 0)
            {
                col.Item().Element(ComposeResidencesSection);
                col.Item().PaddingVertical(4);
            }

            // Section 13: Allowances & Gross Salary (جدول المخصصات والراتب الإجمالي)
            if (_m.ActiveAllowancePeriod != null && _m.ActiveAllowancePeriod.Items.Count > 0)
            {
                col.Item().Element(ComposeAllowancesSection);
            }
        });
    }

    private void ComposeHeroSection(IContainer container)
    {
        container.Border(1).BorderColor(BorderColor).Background(Colors.White).Padding(8).Row(row =>
        {
            // 1. Right side: Professional Employee Portrait Photo (Enlarged to 105, AlignTop to prevent row stretch and hug image tightly)
            row.ConstantItem(105).AlignTop().Element(photoContainer =>
            {
                if (_m.ProfilePhotoBytes != null && _m.ProfilePhotoBytes.Length > 0)
                {
                    photoContainer.Border(1).BorderColor(BorderColor).Image(_m.ProfilePhotoBytes).FitWidth();
                }
                else
                {
                    photoContainer.Height(130).Background(TableHeaderBg).Border(1).BorderColor(BorderColor).AlignCenter().AlignMiddle()
                        .Text(_m.Employee.Name.Length > 0 ? _m.Employee.Name[..1] : "م").Bold().FontSize(28).FontColor(NavyPrimary);
                }
            });

            // 2. Middle: Employee Core Identity & Placement Details
            row.RelativeItem().PaddingHorizontal(10).Column(c =>
            {
                var fullName = (!string.IsNullOrWhiteSpace(_m.Employee.FirstName) || !string.IsNullOrWhiteSpace(_m.Employee.SecondName))
                    ? $"{_m.Employee.FirstName} {_m.Employee.SecondName} {_m.Employee.ThirdName} {_m.Employee.FourthName}".Trim()
                    : _m.Employee.Name;

                c.Item().Text(fullName).Bold().FontSize(13).FontColor(NavyPrimary);

                c.Item().PaddingTop(3).Text(t =>
                {
                    t.Span(_m.Employee.JobTitle?.Name ?? "عنوان غير محدد").Bold().FontSize(9.5f).FontColor(BlueAccent);
                    if (_m.Employee.PositionTitle != null && _m.Employee.PositionTitle.Name != "غير محدد")
                    {
                        t.Span($"  ·  {_m.Employee.PositionTitle.Name}").Bold().FontSize(9f).FontColor(SlateDark);
                    }
                });

                // 3. Administrative Units (التشكيلات الإدارية حسب الهيكلية)
                if (_m.AdministrativeLineage.Count > 0)
                {
                    c.Item().PaddingTop(3).Column(uCol =>
                    {
                        for (var ui = 0; ui < _m.AdministrativeLineage.Count; ui++)
                        {
                            var uName = _m.AdministrativeLineage[ui];
                            uCol.Item().PaddingTop(ui == 0 ? 0 : 2).Text(t =>
                            {
                                t.Span(uName).Bold().FontSize(8.5f).FontColor(NavyPrimary);
                                if (ui == 0 && _m.IsUnitManager)
                                {
                                    var title = string.IsNullOrWhiteSpace(_m.UnitManagerTitle) ? "مسؤول التشكيل" : _m.UnitManagerTitle;
                                    t.Span($" [{title}]").Bold().FontColor(GoldAccent).FontSize(8f);
                                }
                            });
                        }
                    });
                }
                else
                {
                    var fallbackUnit = _m.EffectiveUnit?.Name ?? _m.Employee.Formation?.Name ?? "المركز الرئيسي";
                    c.Item().PaddingTop(3).Text(fallbackUnit).Bold().FontSize(8.5f).FontColor(NavyPrimary);
                }

                c.Item().PaddingTop(3).Row(tagRow =>
                {
                    tagRow.AutoItem().Background(TableHeaderBg).Border(1).BorderColor(BorderColor).PaddingHorizontal(6).PaddingVertical(2).Text(t =>
                    {
                        t.Span("الدرجة: ").FontColor(SlateMuted).FontSize(7.5f);
                        t.Span(_m.Employee.Grade?.Name ?? "—").Bold().FontSize(8f).FontColor(NavyPrimary);
                    });

                    tagRow.AutoItem().PaddingRight(4).Background(TableHeaderBg).Border(1).BorderColor(BorderColor).PaddingHorizontal(6).PaddingVertical(2).Text(t =>
                    {
                        t.Span("المرحلة: ").FontColor(SlateMuted).FontSize(7.5f);
                        t.Span(_m.Employee.Stage?.Name ?? "—").Bold().FontSize(8f).FontColor(NavyPrimary);
                    });

                    tagRow.AutoItem().PaddingRight(4).Background(TableHeaderBg).Border(1).BorderColor(BorderColor).PaddingHorizontal(6).PaddingVertical(2).Text(t =>
                    {
                        t.Span("الخدمة: ").FontColor(SlateMuted).FontSize(7.5f);
                        t.Span($"{_m.ServiceYears} سنة و {_m.ServiceMonths} شهر").Bold().FontSize(8f).FontColor(NavyPrimary);
                    });
                });
            });

            // 3. Left side: Prominent Official QR Code with Opaque Security Token
            row.ConstantItem(100).Column(qrCol =>
            {
                if (_m.QrCodeBytes != null && _m.QrCodeBytes.Length > 0)
                {
                    qrCol.Item().AlignCenter().Width(100).Height(100).Image(_m.QrCodeBytes).FitArea();
                        }
                else
                {
                    qrCol.Item().AlignCenter().Text("CBI VERIFIED").FontSize(8).FontColor(SlateMuted);
                }
            });
        });
    }

    private void ComposePersonalInfoSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, "أولاً: البيانات الشخصية والعائلية"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                ComposeTableRow(table, "اسم الأم واللقب", _m.Employee.MotherName ?? "—", "الجنس", _m.Employee.Gender?.Name ?? "—");
                ComposeTableRow(table, "تاريخ ومحل الولادة", $"{D(_m.Employee.BirthDate)} ({_m.AgeYears} سنة)", "الحالة الاجتماعية", _m.Employee.MaritalStatus?.Name ?? "—");
                ComposeTableRow(table, "الجنسية", _m.Employee.Nationality?.Name ?? "عراقية", "الحالة الوظيفية", _m.Employee.EmployeeStatus?.Name ?? "مستمر بالخدمة");
                ComposeTableRow(table, "الديانة", _m.Employee.Religion?.Name ?? "—", "الدائرة الأولى عند التعيين", _m.Employee.FirstDepartment ?? "—");
                if (!string.IsNullOrWhiteSpace(_m.Employee.UnifiedEmployeeNumber))
                {
                    ComposeTableRow(table, "الرقم الوظيفي الموحد", _m.Employee.UnifiedEmployeeNumber, "—", "—");
                }
            });
        });
    }

    private void ComposeCivilIdentitySection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, "ثانياً: بيانات الأحوال المدنية والتوثيق الموحد"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                var nationalIdVal = !string.IsNullOrWhiteSpace(_m.Employee.NationalId) ? _m.Employee.NationalId : "غير مدخل";
                ComposeTableRow(table, "الرقم الوطني الموحد", nationalIdVal, "حالة القيد والتوثيق", !string.IsNullOrWhiteSpace(_m.Employee.NationalId) ? "بطاقة وطنية مسجلة وموثقة" : "بانتظار استكمال القيد");
            });
        });
    }

    private void ComposeCareerInfoSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, "ثالثاً: البيانات الوظيفية والخدمة الفعلية"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                ComposeTableRow(table, "الدرجة والمرحلة", $"الدرجة {_m.Employee.Grade?.Name ?? "—"} · المرحلة {_m.Employee.Stage?.Name ?? "—"}", "تاريخ آخر ترقية / عنوان", D(_m.Employee.LastJobTitleDate));
                ComposeTableRow(table, "تاريخ التعيين والمباشرة", D(_m.Employee.HireDate), "مدة الخدمة الفعلية", $"{_m.ServiceYears} سنة و {_m.ServiceMonths} شهر و {_m.ServiceDays} يوم");

                var directUnit = _m.AdministrativeLineage.Count > 0 
                    ? _m.AdministrativeLineage[0] 
                    : (_m.EffectiveUnit?.Name ?? _m.Employee.Formation?.Name ?? "المركز الرئيسي");

                var hierarchyPath = _m.AdministrativeLineage.Count > 1
                    ? string.Join("  ←  ", _m.AdministrativeLineage)
                    : directUnit;

                ComposeTableRow(table, "التشكيل الإداري المباشر", directUnit, "سلسلة التبعية الهيكلية", hierarchyPath);
            });
        });
    }

    private void ComposeEducationInfoSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, "رابعاً: التحصيل العلمي والمؤهلات الأكاديمية"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                ComposeTableRow(table, "الشهادة والمؤهل الأكاديمي", _m.Employee.EducationLevel?.Name ?? "—", "التخصص العام / الدقيق", _m.Employee.Specialization?.Name ?? "—");
            });
        });
    }

    private void ComposeContactInfoSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, "خامساً: بيانات الاتصال والتواصل المعتمدة"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(125);
                    cols.RelativeColumn();
                });

                var phoneStr = _m.Phones.Count > 0 ? string.Join(" ، ", _m.Phones.Select(p => p.PhoneNumber)) : "—";
                var emailStr = _m.Emails.Count > 0 ? string.Join(" ، ", _m.Emails.Select(e => e.Email)) : "—";

                table.Cell().Background(TableHeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(3.5f).Text("أرقام الهواتف المعتمدة").Bold().FontSize(8).FontColor(SlateDark);
                table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(3.5f).Text(phoneStr).FontSize(8);

                table.Cell().Background(TableHeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(3.5f).Text("البريد الإلكتروني المؤسسي").Bold().FontSize(8).FontColor(SlateDark);
                table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(3.5f).Text(emailStr).FontSize(8);
            });
        });
    }

    private void ComposeCareerMovementsSection(IContainer container)
    {
        var movements = _m.CareerMovements.OrderByDescending(m => m.MovementDate).ToList();

        container.Column(col =>
        {
            var balText = _m.Employee.LeaveBalance.HasValue
                ? $" · رصيد الإجازات المتبقي: {_m.Employee.LeaveBalance.Value:G29} يوم"
                : "";
            col.Item().Element(e => ComposeSectionTitle(e, $"سادساً: حركة التنقلات والمسار الوظيفي ({movements.Count}){balText}"));

            // Timeline Items (from newest to oldest, without summary, without station labels)
            col.Item().PaddingTop(2).Column(timeline =>
            {
                for (var i = 0; i < movements.Count; i++)
                {
                    var mov = movements[i];
                    var isLast = (i == movements.Count - 1);

                    var countedTerms = new List<string>(3);
                    if (mov.CountForAllowance) countedTerms.Add("علاوة");
                    if (mov.CountForPromotion) countedTerms.Add("ترفيع");
                    if (mov.CountForPension) countedTerms.Add("تقاعد");

                    var countedText = countedTerms.Count > 0
                        ? string.Join(" · ", countedTerms)
                        : "غير محتسبة";

                    timeline.Item().Row(row =>
                    {
                        // 1. Right: Date only
                        row.ConstantItem(68).PaddingTop(2).Column(dc =>
                        {
                            dc.Item().Text(D(mov.MovementDate)).Bold().FontSize(8.5f).FontColor(NavyPrimary);
                        });

                        // 2. Middle: Visual Timeline Track & Bullet
                        row.ConstantItem(14).PaddingTop(2).Column(tc =>
                        {
                            tc.Item().AlignCenter().Text("●").FontSize(9.5f).FontColor(GoldAccent);
                            if (!isLast)
                            {
                                tc.Item().AlignCenter().Width(1.2f).MinHeight(22).Background(BorderColor);
                            }
                        });

                        // 3. Left: Movement Details Card
                        row.RelativeItem().PaddingBottom(isLast ? 0 : 5).Background(CardBg).Border(0.6f).BorderColor(BorderColor).Padding(5).Column(card =>
                        {
                            card.Item().Row(cr =>
                            {
                                cr.RelativeItem().Text(mov.FormationName).Bold().FontSize(8.5f).FontColor(SlateDark);

                                cr.AutoItem().Background(Colors.White).Border(0.5f).BorderColor(BorderColor).PaddingHorizontal(5).PaddingVertical(1.5f).Text(t =>
                                {
                                    t.Span(countedText).Bold().FontSize(7f).FontColor(countedTerms.Count > 0 ? Colors.Green.Darken2 : SlateMuted);
                                });
                            });

                            if (!string.IsNullOrWhiteSpace(mov.Notes))
                            {
                                card.Item().PaddingTop(2.5f).Text(t =>
                                {
                                    t.Span("الملاحظات / الأمر الإداري: ").FontSize(7f).FontColor(SlateMuted);
                                    t.Span(mov.Notes).FontSize(7.5f).FontColor(SlateDark);
                                });
                            }
                        });
                    });
                }
            });
        });
    }

    private void ComposeLeavesSection(IContainer container)
    {
        container.Column(c =>
        {
            var balanceText = _m.Employee.LeaveBalance.HasValue
                ? $" · رصيد الإجازات المتبقي: {_m.Employee.LeaveBalance.Value:G29} يوم"
                : "";
            c.Item().Element(e => ComposeSectionTitle(e, $"سابعاً: سجل الإجازات الرسمية المعتمدة ({_m.Leaves.Count}){balanceText}"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(20);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(35);
                    cols.RelativeColumn(2.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("نوع الإجازة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("من تاريخ").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("إلى تاريخ").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("المدة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الحالة والأمر").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var leave in _m.Leaves)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(leave.LeaveType?.Name ?? "إجازة").FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(D(leave.StartDate)).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(D(leave.EndDate)).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text($"{leave.DaysCount} يوم").FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(leave.Status).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposeCoursesSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, $"ثامناً: سجل الدورات التدريبية والتأهيل ({_m.Courses.Count})"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(20);
                    cols.RelativeColumn(4);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(35);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("اسم الدورة التدريبية").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("تاريخ البدء").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("تاريخ الانتهاء").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("المدة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الحالة").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var course in _m.Courses)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(course.CourseName).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(D(course.StartDate)).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(D(course.EndDate)).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text($"{course.DaysCount?.ToString() ?? "—"} يوم").FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(course.Status).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposeAppreciationLettersSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, $"تاسعاً: سجل كتب الشكر والتقدير والتكريم ({_m.AppreciationLetters.Count})"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(20);
                    cols.RelativeColumn(2.5f);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2.5f);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("رقم وتاريخ الكتاب").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("عنوان / موضوع التكريم").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("نوع الكتاب").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("القِدم الممنوح").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var letter in _m.AppreciationLetters)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    var letterNumStr = !string.IsNullOrWhiteSpace(letter.LetterNumber) ? letter.LetterNumber : letter.Id.ToString();
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text($"{letterNumStr} ({D(letter.LetterDate)})").FontSize(7.5f);
                    var letterTitleStr = letter.TitleLookup?.Name ?? letter.Title ?? "شكر وتقدير";
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(letterTitleStr).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(letter.Type?.Name ?? "الإدارة العامة").FontSize(7.5f);

                    var seniority = "—";
                    if (letter.Type != null && (letter.Type.SeniorityYears > 0 || letter.Type.SeniorityMonths > 0 || letter.Type.SeniorityDays > 0))
                    {
                        var parts = new List<string>();
                        if (letter.Type.SeniorityYears > 0) parts.Add($"{letter.Type.SeniorityYears} سنة");
                        if (letter.Type.SeniorityMonths > 0) parts.Add($"{letter.Type.SeniorityMonths} شهر");
                        if (letter.Type.SeniorityDays > 0) parts.Add($"{letter.Type.SeniorityDays} يوم");
                        seniority = string.Join(" و ", parts);
                    }
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(seniority).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposePenaltiesSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, $"عاشراً: سجل العقوبات الانضباطية ({_m.Penalties.Count})"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(18);
                    cols.RelativeColumn(2.2f);
                    cols.RelativeColumn(1.8f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(2.5f);
                    cols.RelativeColumn(2.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("العقوبة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("تاريخ نيل العقوبة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("رقم الكتاب").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الجهة / التشكيل").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الملاحظات").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var pen in _m.Penalties)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(pen.PenaltyType?.Name ?? "عقوبة").Bold().FontSize(7.5f).FontColor(Colors.Red.Darken2);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(D(pen.PenaltyDate)).FontSize(7.5f);

                    var orderStr = !string.IsNullOrWhiteSpace(pen.OrderNumber) ? pen.OrderNumber : "—";
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(orderStr).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(GetPenaltyFormation(pen)).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(string.IsNullOrWhiteSpace(pen.Notes) ? "—" : pen.Notes).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposeMartyrsSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, $"بيانات ذوي الشهداء ({_m.Martyrs.Count})"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(20);
                    cols.RelativeColumn(3f);
                    cols.RelativeColumn(3.5f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(2.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("اسم الشهيد").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("نوع الشهادة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("صلة القرابة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("رقم القرار المعتمد").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var martyr in _m.Martyrs)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(martyr.MartyrName).Bold().FontSize(7.5f).FontColor(NavyPrimary);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(martyr.MartyrdomType?.Name ?? "شهيد").FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(martyr.Kinship?.Name ?? "—").Bold().FontSize(7.5f).FontColor(Colors.Red.Darken2);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(string.IsNullOrWhiteSpace(martyr.DecisionNumber) ? "—" : martyr.DecisionNumber).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposeResidencesSection(IContainer container)
    {
        container.Column(c =>
        {
            c.Item().Element(e => ComposeSectionTitle(e, $"معلومات السكن وبطاقة السكن ({_m.Residences.Count})"));

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(18);
                    cols.RelativeColumn(1.8f);
                    cols.RelativeColumn(1.8f);
                    cols.RelativeColumn(3.5f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(2.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("صفة السكن").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("المحافظة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("العنوان المعتمد").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("مكتب المعلومات").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("رب الأسرة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الاستمارة").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("الفترة الزمنية").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var res in _m.Residences)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(res.IsCurrent ? "السكن الحالي" : "سكن سابق").Bold().FontSize(7.5f).FontColor(res.IsCurrent ? Colors.Green.Darken2 : SlateMuted);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(res.Governorate?.Name ?? "—").Bold().FontSize(7.5f).FontColor(NavyPrimary);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(res.FormattedAddress).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(res.InformationOffice ?? "—").FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(res.FamilyHeadName ?? "—").FontSize(7.5f);
                    
                    var formInfo = !string.IsNullOrWhiteSpace(res.FormNumber) 
                        ? (res.FormDate.HasValue ? $"{res.FormNumber} ({res.FormDate.Value:yyyy/MM/dd})" : res.FormNumber)
                        : "—";
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(formInfo).FontSize(7.5f);
                    
                    var period = $"من: {res.FromDate?.ToString("yyyy/MM/dd") ?? "—"} إلى: {res.ToDate?.ToString("yyyy/MM/dd") ?? "مستمر"}";
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(period).FontSize(7f);
                }
            });
        });
    }

    private string GetPenaltyFormation(EmployeePenalty pen)
    {
        if (pen.PenaltyDate.HasValue && _m.CareerMovements.Count > 0)
        {
            var pDate = pen.PenaltyDate.Value;
            var ordered = _m.CareerMovements.OrderBy(m => m.MovementDate).ToList();
            var today = DateOnly.FromDateTime(DateTime.Today);

            for (var i = 0; i < ordered.Count; i++)
            {
                var cur = ordered[i];
                var start = cur.MovementDate;
                var end = (i < ordered.Count - 1)
                    ? (ordered[i + 1].MovementDate > start ? ordered[i + 1].MovementDate.AddDays(-1) : start)
                    : (today >= start ? today : start);

                if (pDate >= start && pDate <= end && !string.IsNullOrWhiteSpace(cur.FormationName))
                {
                    return cur.FormationName;
                }
            }

            var prior = ordered.Where(m => m.MovementDate <= pDate).OrderByDescending(m => m.MovementDate).FirstOrDefault();
            if (prior != null && !string.IsNullOrWhiteSpace(prior.FormationName))
            {
                return prior.FormationName;
            }
        }

        return _m.Employee.Formation?.Name ?? "—";
    }

    private void ComposeAllowancesSection(IContainer container)
    {
        var period = _m.ActiveAllowancePeriod;
        if (period == null) return;

        container.Column(c =>
        {
            var headerText = $"حادي عشر: جدول المخصصات والراتب الإجمالي الشهري (ساري المفعول من {D(period.StartDate)})";
            if (!string.IsNullOrWhiteSpace(period.OrderNumber))
            {
                headerText += $" — أمر إداري: {period.OrderNumber}";
            }
            c.Item().Element(e => ComposeSectionTitle(e, headerText));

            // Summary Metrics Box
            c.Item().PaddingBottom(4).Background(CardBg).Border(0.6f).BorderColor(BorderColor).Padding(6).Row(r =>
            {
                r.RelativeItem().Column(sc =>
                {
                    sc.Item().Text("الراتب الإسمي الأساسي").FontSize(7f).FontColor(SlateMuted);
                    sc.Item().Text($"{period.BasicSalary:N0} د.ع").Bold().FontSize(8.5f).FontColor(NavyPrimary);
                });

                r.RelativeItem().Column(sc =>
                {
                    sc.Item().Text($"المخصصات النسبية ({period.TotalPercentageRate:G29}%)").FontSize(7f).FontColor(SlateMuted);
                    sc.Item().Text($"{period.TotalPercentageAmount:N0} د.ع").Bold().FontSize(8.5f).FontColor(BlueAccent);
                });

                r.RelativeItem().Column(sc =>
                {
                    sc.Item().Text("المخصصات الثابتة (المقطوعة)").FontSize(7f).FontColor(SlateMuted);
                    sc.Item().Text($"{period.TotalFixedAmount:N0} د.ع").Bold().FontSize(8.5f).FontColor(GoldAccent);
                });

                r.RelativeItem().Column(sc =>
                {
                    sc.Item().Text("إجمالي الراتب والمخصصات").FontSize(7f).FontColor(SlateMuted);
                    sc.Item().Text($"{period.GrossSalary:N0} د.ع").Bold().FontSize(9.5f).FontColor(Colors.Green.Darken2);
                });
            });

            // Detailed Allowances Table
            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(20);
                    cols.RelativeColumn(3.5f);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2.5f);
                    cols.RelativeColumn(3);
                });

                table.Header(h =>
                {
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("ت").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("اسم المخصص").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("نوع الاحتساب").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignCenter().Text("النسبة / المبلغ").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).AlignLeft().Text("المبلغ المحتسب شهرياً").Bold().FontSize(7.5f).FontColor(Colors.White);
                    h.Cell().Background(NavyPrimary).Padding(3).Text("السند / الملاحظات").Bold().FontSize(7.5f).FontColor(Colors.White);
                });

                var idx = 1;
                foreach (var item in period.Items)
                {
                    var bg = (idx % 2 == 0) ? RowAltBg : RowWhite;
                    var typeLabel = item.CalculationType == API.HR.Node.Data.Allowances.AllowanceCalculationType.Percentage ? "نسبية" : "مقطوعة";
                    var rateOrVal = item.CalculationType == API.HR.Node.Data.Allowances.AllowanceCalculationType.Percentage 
                        ? $"{item.Percentage:G29}%" 
                        : $"{item.FixedAmount ?? item.CalculatedAmount:N0} د.ع";

                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(idx++.ToString()).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(item.Name).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(typeLabel).FontSize(7.5f);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignCenter().Text(rateOrVal).Bold().FontSize(7.5f).FontColor(NavyPrimary);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).AlignLeft().Text($"{item.CalculatedAmount:N0} د.ع").Bold().FontSize(7.5f).FontColor(Colors.Green.Darken2);
                    table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(2.5f).Text(string.IsNullOrWhiteSpace(item.Notes) ? "—" : item.Notes).FontSize(7.5f);
                }
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.PaddingTop(6).BorderTop(1).BorderColor(BorderColor).Row(row =>
        {
            row.RelativeItem().Text("البنك المركزي العراقي — مديرية شؤون الموارد البشرية").FontSize(7.5f).FontColor(SlateMuted);
            row.RelativeItem().AlignCenter().Text($"تاريخ الإصدار: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(7.5f).FontColor(SlateMuted);
            row.RelativeItem().AlignLeft().Text(t =>
            {
                t.Span("صفحة ");
                t.CurrentPageNumber();
                t.Span(" من ");
                t.TotalPages();
            });
        });
    }

    private static void ComposeSectionTitle(IContainer container, string title)
    {
        container.PaddingBottom(2).Row(r =>
        {
            r.AutoItem().Background(NavyPrimary).Width(3).Height(11);
            r.RelativeItem().PaddingRight(4).Text(title).Bold().FontSize(9f).FontColor(NavyPrimary);
        });
    }

    private static void ComposeTableRow(TableDescriptor table, string label1, string val1, string label2, string val2)
    {
        table.Cell().Background(TableHeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(3f).Text(label1).Bold().FontSize(8).FontColor(SlateDark);
        table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(3f).Text(val1).FontSize(8);

        table.Cell().Background(TableHeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(3f).Text(label2).Bold().FontSize(8).FontColor(SlateDark);
        table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(3f).Text(val2).FontSize(8);
    }

    private static string D(DateOnly? d) => d?.ToString("yyyy/MM/dd") ?? "—";
}
