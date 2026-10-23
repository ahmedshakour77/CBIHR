namespace API.HR.Node.Services.ProfilePhotos;

/// <summary>
/// Browser URLs for the profile-photo endpoints. Pages point an &lt;img&gt; at
/// these instead of copying the bytes into the circuit as base64: the browser
/// fetches lazily, revalidates with the ETag, and the endpoint audits the read
/// against the real client address.
/// </summary>
/// <remarks>
/// The version in the query string changes the src when a photo is replaced, so
/// Blazor's diff swaps the image. It is not a cache key on its own (versions
/// restart at 1 after a delete); the endpoint's <c>no-cache</c> revalidation is.
/// Paths are relative so they resolve against &lt;base href&gt;.
/// </remarks>
public static class ProfilePhotoUrls
{
    public static string Thumbnail(string employeeID, int? version) =>
        $"api/employees/{Uri.EscapeDataString(employeeID)}/profile-photo/thumbnail?v={version ?? 1}";

    public static string Original(string employeeID, int? version) =>
        $"api/employees/{Uri.EscapeDataString(employeeID)}/profile-photo?v={version ?? 1}";
}
