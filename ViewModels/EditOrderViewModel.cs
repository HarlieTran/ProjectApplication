using System.ComponentModel.DataAnnotations;

namespace ProjectApplication.ViewModels
{
    public class EditOrderViewModel
    {
        public int Id { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string AuditLog { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<EditOrderItemViewModel> Items { get; set; } = new();
    }

    public class EditOrderItemViewModel
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal => UnitPrice * Quantity;
    }
}
