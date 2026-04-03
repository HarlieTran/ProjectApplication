namespace ProjectApplication.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }

        public int AdminCount { get; set; }

        public int ManagerCount { get; set; }

        public int StandardUserCount { get; set; }

        public int LockedOutUsers { get; set; }

        public List<AdminUserListItemViewModel> Users { get; set; } = [];
    }

    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsLockedOut { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public bool IsCurrentUser { get; set; }
    }
}
