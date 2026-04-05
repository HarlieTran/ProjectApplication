using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApplication.Models;
using ProjectApplication.ViewModels;

namespace ProjectApplication.Controllers
{
    [Authorize(Roles = "Manager, Admin")]
    public class ManagerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ManagerController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: Manager/Index
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var currentUserId = _userManager.GetUserId(User);
            var userList = new List<AdminUserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLockedOut = await _userManager.IsLockedOutAsync(user);

                userList.Add(new AdminUserListItemViewModel
                {
                    Id = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "User",
                    IsLockedOut = isLockedOut,
                    LockoutEnd = user.LockoutEnd,
                    IsCurrentUser = user.Id == currentUserId
                });
            }

            // Create the dashboard view model
            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = users.Count,
                AdminCount = userList.Count(u => u.Role == "Admin"),
                ManagerCount = userList.Count(u => u.Role == "Manager"),
                StandardUserCount = userList.Count(u => u.Role == "User"),
                LockedOutUsers = userList.Count(u => u.IsLockedOut),
                Users = userList
            };

            return View(viewModel);
        }
    }
}