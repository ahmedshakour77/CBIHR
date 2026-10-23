using Microsoft.AspNetCore.Identity;

namespace API.HR.Node.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string? EmployeeID { get; set; }
        public Employee? Employee { get; set; }

        public int? FormationId { get; set; }
        public Formation? Formation { get; set; }
    }
}
