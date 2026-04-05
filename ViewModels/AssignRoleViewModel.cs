using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ProjectApplication.ViewModels
{
    public class AssignRoleViewModel
    {
        [Required(ErrorMessage = "Please select a user")]
        [Display(Name = "User")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a role")]
        [Display(Name = "Role")]
        public string RoleName { get; set; } = string.Empty;

        public List<SelectListItem> Users { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
    }
}