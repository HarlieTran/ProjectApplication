using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectApplication.Models;
using ProjectApplication.ViewModels;
using System.Security.Claims;

namespace ProjectApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ----------------------------------------------------------------------- 
        // USER MANAGEMENT ACTIONS 
        // -----------------------------------------------------------------------

        // GET: /Admin
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ThenBy(user => user.Email)
                .ToListAsync();

            var currentUserId = _userManager.GetUserId(User);
            var userItems = new List<AdminUserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLockedOut = await _userManager.IsLockedOutAsync(user);

                userItems.Add(new AdminUserListItemViewModel
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

            var model = new AdminDashboardViewModel
            {
                TotalUsers = userItems.Count,
                AdminCount = userItems.Count(user => user.Role == "Admin"),
                ManagerCount = userItems.Count(user => user.Role == "Manager"),
                StandardUserCount = userItems.Count(user => user.Role == "User"),
                LockedOutUsers = userItems.Count(user => user.IsLockedOut),
                Users = userItems
            };

            return View(model);
        }

        // GET: /Admin/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            await PopulateRolesAsync(model);
            return View(model);
        }

        // POST: /Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            var normalizedEmail = model.Email?.Trim() ?? string.Empty;
            var normalizedFirstName = model.FirstName?.Trim() ?? string.Empty;
            var normalizedLastName = model.LastName?.Trim() ?? string.Empty;
            var selectedRole = model.SelectedRole?.Trim() ?? string.Empty;

            model.Email = normalizedEmail;
            model.FirstName = normalizedFirstName;
            model.LastName = normalizedLastName;
            model.SelectedRole = selectedRole;

            if (!await _roleManager.RoleExistsAsync(selectedRole))
            {
                ModelState.AddModelError(nameof(model.SelectedRole), "Please choose a valid role.");
            }

            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

            if (existingUser is not null)
            {
                ModelState.AddModelError(nameof(model.Email), "Another account already uses this email address.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateRolesAsync(model);
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);

            if (!createResult.Succeeded)
            {
                AddErrorsToModelState(createResult);
                await PopulateRolesAsync(model);
                return View(model);
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, selectedRole);

            if (!addRoleResult.Succeeded)
            {
                AddErrorsToModelState(addRoleResult);
                await PopulateRolesAsync(model);
                return View(model);
            }

            var claimResult = await ReplaceFullNameClaimAsync(user);

            if (!claimResult.Succeeded)
            {
                AddErrorsToModelState(claimResult);
                await PopulateRolesAsync(model);
                return View(model);
            }

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = "User account created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            var model = await BuildEditUserViewModelAsync(user);
            return View(model);
        }

        // POST: /Admin/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);

            if (user is null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var normalizedEmail = model.Email?.Trim() ?? string.Empty;
            var normalizedFirstName = model.FirstName?.Trim() ?? string.Empty;
            var normalizedLastName = model.LastName?.Trim() ?? string.Empty;
            var selectedRole = model.SelectedRole?.Trim() ?? string.Empty;

            model.Email = normalizedEmail;
            model.FirstName = normalizedFirstName;
            model.LastName = normalizedLastName;
            model.SelectedRole = selectedRole;

            if (!await _roleManager.RoleExistsAsync(selectedRole))
            {
                ModelState.AddModelError(nameof(model.SelectedRole), "Please choose a valid role.");
            }

            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

            if (existingUser is not null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "Another account already uses this email address.");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault() ?? "User";

            if (user.Id == currentUserId && selectedRole != "Admin")
            {
                ModelState.AddModelError(nameof(model.SelectedRole), "You cannot remove your own Admin role.");
            }

            if (currentRole == "Admin" && selectedRole != "Admin")
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

                if (adminUsers.Count <= 1)
                {
                    ModelState.AddModelError(nameof(model.SelectedRole), "At least one Admin account must remain in the system.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateRolesAsync(model);
                model.IsCurrentUser = user.Id == currentUserId;
                model.IsLockedOut = await _userManager.IsLockedOutAsync(user);
                return View(model);
            }

            user.FirstName = normalizedFirstName;
            user.LastName = normalizedLastName;
            user.Email = normalizedEmail;
            user.UserName = normalizedEmail;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                AddErrorsToModelState(updateResult);
                await PopulateRolesAsync(model);
                model.IsCurrentUser = user.Id == currentUserId;
                model.IsLockedOut = await _userManager.IsLockedOutAsync(user);
                return View(model);
            }

            if (currentRole != selectedRole)
            {
                if (currentRoles.Count > 0)
                {
                    var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    if (!removeRolesResult.Succeeded)
                    {
                        AddErrorsToModelState(removeRolesResult);
                        await PopulateRolesAsync(model);
                        model.IsCurrentUser = user.Id == currentUserId;
                        model.IsLockedOut = await _userManager.IsLockedOutAsync(user);
                        return View(model);
                    }
                }

                var addRoleResult = await _userManager.AddToRoleAsync(user, selectedRole);

                if (!addRoleResult.Succeeded)
                {
                    AddErrorsToModelState(addRoleResult);
                    await PopulateRolesAsync(model);
                    model.IsCurrentUser = user.Id == currentUserId;
                    model.IsLockedOut = await _userManager.IsLockedOutAsync(user);
                    return View(model);
                }
            }

            var claimResult = await ReplaceFullNameClaimAsync(user);

            if (!claimResult.Succeeded)
            {
                AddErrorsToModelState(claimResult);
                await PopulateRolesAsync(model);
                model.IsCurrentUser = user.Id == currentUserId;
                model.IsLockedOut = await _userManager.IsLockedOutAsync(user);
                return View(model);
            }

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = "User details updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/ResetPassword/{id}
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            var model = new ResetPasswordViewModel
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Email = user.Email ?? string.Empty
            };

            return View(model);
        }

        // POST: /Admin/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);

            if (user is null)
            {
                return NotFound();
            }

            model.FullName = $"{user.FirstName} {user.LastName}".Trim();
            model.Email = user.Email ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!result.Succeeded)
            {
                AddErrorsToModelState(result);
                return View(model);
            }

            await _userManager.UpdateSecurityStampAsync(user);

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = "Password reset successfully.";
            return RedirectToAction(nameof(Edit), new { id = user.Id });
        }

        // POST: /Admin/ToggleLock/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            if (user.Id == currentUserId)
            {
                TempData["StatusType"] = "danger";
                TempData["StatusMessage"] = "You cannot lock your own account.";
                return RedirectToAction(nameof(Index));
            }

            var isLockedOut = await _userManager.IsLockedOutAsync(user);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isLockedOut && isAdmin)
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

                if (adminUsers.Count <= 1)
                {
                    TempData["StatusType"] = "danger";
                    TempData["StatusMessage"] = "You cannot lock the only remaining Admin account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            IdentityResult result;

            if (isLockedOut)
            {
                result = await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["StatusType"] = result.Succeeded ? "success" : "danger";
                TempData["StatusMessage"] = result.Succeeded
                    ? "User account unlocked successfully."
                    : "Unable to unlock the selected user.";
            }
            else
            {
                var enableLockoutResult = await _userManager.SetLockoutEnabledAsync(user, true);

                if (!enableLockoutResult.Succeeded)
                {
                    TempData["StatusType"] = "danger";
                    TempData["StatusMessage"] = "Unable to enable lockout for the selected user.";
                    return RedirectToAction(nameof(Index));
                }

                result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["StatusType"] = result.Succeeded ? "warning" : "danger";
                TempData["StatusMessage"] = result.Succeeded
                    ? "User account locked successfully."
                    : "Unable to lock the selected user.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            if (user.Id == currentUserId)
            {
                TempData["StatusType"] = "danger";
                TempData["StatusMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

                if (adminUsers.Count <= 1)
                {
                    TempData["StatusType"] = "danger";
                    TempData["StatusMessage"] = "You cannot delete the only remaining Admin account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var deleteResult = await _userManager.DeleteAsync(user);

            TempData["StatusType"] = deleteResult.Succeeded ? "success" : "danger";
            TempData["StatusMessage"] = deleteResult.Succeeded
                ? "User deleted successfully."
                : string.Join(" ", deleteResult.Errors.Select(error => error.Description));

            return RedirectToAction(nameof(Index));
        }

        // ----------------------------------------------------------------------- 
        // ROLE MANAGEMENT ACTIONS 
        // -----------------------------------------------------------------------

        // GET: Admin/Roles
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var roleViewModels = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);

                roleViewModels.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    UserCount = usersInRole.Count,
                    Users = (List<ApplicationUser>)usersInRole
                });
            }

            return View(roleViewModels);
        }

        // GET: Admin/CreateRole
        public IActionResult CreateRole()
        {
            return View();
        }

        // POST: Admin/CreateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(CreateRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if role already exists
                var roleExists = await _roleManager.RoleExistsAsync(model.RoleName);
                if (roleExists)
                {
                    ModelState.AddModelError("", "Role already exists");
                    return View(model);
                }

                // Create the role
                var result = await _roleManager.CreateAsync(new IdentityRole(model.RoleName));

                if (result.Succeeded)
                {
                    TempData["Success"] = $"Role '{model.RoleName}' created successfully";
                    return RedirectToAction(nameof(Roles));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        // GET: Admin/EditRole/{id}
        public async Task<IActionResult> EditRole(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);

            var model = new EditRoleViewModel
            {
                Id = role.Id,
                RoleName = role.Name,
                Users = usersInRole.Select(u => u.UserName).ToList()
            };

            return View(model);
        }

        // POST: Admin/EditRole/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(EditRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var role = await _roleManager.FindByIdAsync(model.Id);
                if (role == null)
                {
                    return NotFound();
                }

                role.Name = model.RoleName;
                var result = await _roleManager.UpdateAsync(role);

                if (result.Succeeded)
                {
                    TempData["Success"] = $"Role updated successfully";
                    return RedirectToAction(nameof(Roles));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        // POST: Admin/DeleteRole/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            // Check if any users are in this role
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
            {
                TempData["Error"] = $"Cannot delete role '{role.Name}' because it has {usersInRole.Count} user(s) assigned to it.";
                return RedirectToAction(nameof(Roles));
            }

            var result = await _roleManager.DeleteAsync(role);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{role.Name}' deleted successfully";
            }
            else
            {
                TempData["Error"] = "Failed to delete role";
            }

            return RedirectToAction(nameof(Roles));
        }

        // GET: Admin/AssignRole
        public async Task<IActionResult> AssignRole()
        {
            var users = await _userManager.Users.ToListAsync();
            var roles = await _roleManager.Roles.ToListAsync();

            var model = new AssignRoleViewModel
            {
                Users = users.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.UserName} ({u.Email})"
                }).ToList(),
                Roles = roles.Select(r => new SelectListItem
                {
                    Value = r.Name,
                    Text = r.Name
                }).ToList()
            };

            return View(model);
        }

        // POST: Admin/AssignRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found");
                }
                else
                {
                    // Check if user already has this role
                    var isInRole = await _userManager.IsInRoleAsync(user, model.RoleName);
                    if (isInRole)
                    {
                        TempData["Error"] = $"User '{user.UserName}' already has the role '{model.RoleName}'";
                    }
                    else
                    {
                        var result = await _userManager.AddToRoleAsync(user, model.RoleName);

                        if (result.Succeeded)
                        {
                            TempData["Success"] = $"Role '{model.RoleName}' assigned to '{user.UserName}' successfully";
                            return RedirectToAction(nameof(AssignRole));
                        }

                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
            }

            // Reload dropdowns if validation fails
            var users = await _userManager.Users.ToListAsync();
            var roles = await _roleManager.Roles.ToListAsync();

            model.Users = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.UserName} ({u.Email})"
            }).ToList();
            model.Roles = roles.Select(r => new SelectListItem
            {
                Value = r.Name,
                Text = r.Name
            }).ToList();

            return View(model);
        }

        // POST: Admin/RemoveUserFromRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUserFromRole(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found";
                return RedirectToAction(nameof(Roles));
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{roleName}' removed from '{user.UserName}' successfully";
            }
            else
            {
                TempData["Error"] = "Failed to remove role from user";
            }

            return RedirectToAction(nameof(Roles));
        }

        // GET: Admin/ManageUserRoles/{id}
        public async Task<IActionResult> ManageUserRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.ToListAsync();

            var model = new UserRoleViewModel
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Roles = userRoles.ToList()
            };

            ViewBag.AllRoles = allRoles.Select(r => r.Name).ToList();
            ViewBag.AvailableRoles = allRoles.Where(r => !userRoles.Contains(r.Name)).Select(r => r.Name).ToList();

            return View(model);
        }

        // Helper methods

        // Builds an EditUserViewModel for the specified user, including their current role and lockout status.
        private async Task<EditUserViewModel> BuildEditUserViewModelAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                SelectedRole = roles.FirstOrDefault() ?? "User",
                IsLockedOut = await _userManager.IsLockedOutAsync(user),
                IsCurrentUser = user.Id == _userManager.GetUserId(User)
            };

            await PopulateRolesAsync(model);
            return model;
        }

        // Populates the AvailableRoles property of the view model with the list of roles from the database, ordered alphabetically.
        private async Task PopulateRolesAsync(CreateUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles
                .OrderBy(role => role.Name)
                .Select(role => role.Name ?? string.Empty)
                .ToListAsync();
        }

        // Overload for EditUserViewModel
        private async Task PopulateRolesAsync(EditUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles
                .OrderBy(role => role.Name)
                .Select(role => role.Name ?? string.Empty)
                .ToListAsync();
        }

        // Replaces the "FullName" claim for the specified user with a new claim that combines their current first and last name. If the user does not have an existing "FullName" claim, a new one will be added.
        private async Task<IdentityResult> ReplaceFullNameClaimAsync(ApplicationUser user)
        {
            var existingClaims = await _userManager.GetClaimsAsync(user);
            var nameClaims = existingClaims.Where(claim => claim.Type == "FullName").ToList();

            if (nameClaims.Count > 0)
            {
                var removeResult = await _userManager.RemoveClaimsAsync(user, nameClaims);

                if (!removeResult.Succeeded)
                {
                    return removeResult;
                }
            }

            return await _userManager.AddClaimAsync(
                user,
                new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim()));
        }

        // Adds the errors from an IdentityResult to the ModelState, allowing them to be displayed in the view.
        private void AddErrorsToModelState(IdentityResult identityResult)
        {
            foreach (var error in identityResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
