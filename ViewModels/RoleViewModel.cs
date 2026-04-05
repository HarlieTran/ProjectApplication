using ProjectApplication.Models;

namespace ProjectApplication.ViewModels
{
    public class RoleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public List<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    }
}