namespace API.HR.Node.Data;

/// <summary>
/// What an employee email address is for. Stored as an int; new members must be
/// appended so existing rows keep their meaning.
/// </summary>
public enum EmailLabel
{
    Work = 0,
    Personal = 1,
    Official = 2,
    Other = 3
}

/// <summary>
/// What an employee phone number is for. Stored as an int; new members must be
/// appended so existing rows keep their meaning.
/// </summary>
public enum PhoneLabel
{
    Mobile = 0,
    Work = 1,
    Landline = 2,
    WhatsApp = 3,
    Emergency = 4,
    Other = 5
}

public static class ContactLabelNames
{
    public static string Arabic(EmailLabel label) => label switch
    {
        EmailLabel.Work => "بريد العمل",
        EmailLabel.Personal => "بريد شخصي",
        EmailLabel.Official => "بريد رسمي",
        _ => "أخرى"
    };

    public static string Arabic(PhoneLabel label) => label switch
    {
        PhoneLabel.Mobile => "موبايل",
        PhoneLabel.Work => "هاتف العمل",
        PhoneLabel.Landline => "أرضي",
        PhoneLabel.WhatsApp => "واتساب",
        PhoneLabel.Emergency => "طوارئ",
        _ => "أخرى"
    };
}
