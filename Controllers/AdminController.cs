using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApplication.Models;
using ProjectApplication.ViewModels;

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

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            await PopulateRolesAsync(model);
            return View(model);
        }

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

        private async Task PopulateRolesAsync(CreateUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles
                .OrderBy(role => role.Name)
                .Select(role => role.Name ?? string.Empty)
                .ToListAsync();
        }

        private async Task PopulateRolesAsync(EditUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles
                .OrderBy(role => role.Name)
                .Select(role => role.Name ?? string.Empty)
                .ToListAsync();
        }

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

        private void AddErrorsToModelState(IdentityResult identityResult)
        {
            foreach (var error in identityResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
