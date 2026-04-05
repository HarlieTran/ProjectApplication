using System.ComponentModel.DataAnnotations;

namespace ProjectApplication.ViewModels
{
    public class EditRoleViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role name is required")]
        [Display(Name = "Role Name")]
        public string RoleName { get; set; } = string.Empty;

        public List<string> Users { get; set; } = new List<string>();
    }
}
