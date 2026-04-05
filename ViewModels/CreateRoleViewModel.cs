using System.ComponentModel.DataAnnotations;

namespace ProjectApplication.ViewModels
{
    public class CreateRoleViewModel
    {
        [Required(ErrorMessage = "Role name is required")]
        [Display(Name = "Role Name")]
        public string RoleName { get; set; } = string.Empty;
    }
}