using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApplication.Data;
using ProjectApplication.Models;
using ProjectApplication.ViewModels;

namespace ProjectApplication.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Order - show current user's orders
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Order/All - Admin/Manager can see all orders
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> All()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Order/Edit/{id}
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
                return NotFound();

            var model = new EditOrderViewModel
            {
                Id = order.Id,
                UserFullName = $"{order.User.FirstName} {order.User.LastName}".Trim(),
                UserEmail = order.User.Email,
                CreatedAt = order.CreatedAt,
                AuditLog = order.AuditLog,
                TotalAmount = order.TotalAmount,
                Items = order.Items.Select(i => new EditOrderItemViewModel
                {
                    Id = i.Id,
                    ProductName = i.Product.Name,
                    Category = i.Product.Category,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            return View(model);
        }

        // POST: /Order/Edit
        [Authorize(Roles = "Admin, Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditOrderViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == model.Id);

            if (order is null)
                return NotFound();

            foreach (var itemModel in model.Items)
            {
                var item = order.Items.FirstOrDefault(i => i.Id == itemModel.Id);
                if (item is null)
                    continue;

                if (itemModel.Quantity < 1)
                    itemModel.Quantity = 1;

                item.Quantity = itemModel.Quantity;
            }

            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

            var adminUser = await _userManager.GetUserAsync(User);
            var adminName = adminUser is null
                ? User.Identity?.Name ?? "Admin"
                : $"{adminUser.FirstName} {adminUser.LastName}".Trim();

            order.AuditLog = $"{adminName} updated Order #{order.Id} at {DateTime.UtcNow.ToLocalTime():hh:mm tt} on {DateTime.UtcNow.ToLocalTime():MMM d, yyyy}";

            await _context.SaveChangesAsync();

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = order.AuditLog;
            return RedirectToAction(nameof(Detail), new { id = order.Id });
        }

        // GET: /Order/DeleteConfirm/{id}
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
                return NotFound();

            return View(order);
        }

        // POST: /Order/Delete/{id}
        [Authorize(Roles = "Admin, Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
                return NotFound();

            _context.OrderItems.RemoveRange(order.Items);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = $"Order #{id} was deleted.";
            return RedirectToAction(nameof(All));
        }

        // GET: /Order/Create?productId=1
        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product is null || !product.IsAvailable)
                return NotFound();

            return View(product);
        }

        // POST: /Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int quantity)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products.FindAsync(productId);

            if (product is null || !product.IsAvailable)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (user is null)
                return Unauthorized();

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            var total = product.Price * quantity;

            var order = new Order
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = total,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = quantity,
                        UnitPrice = product.Price
                    }
                }
            };

            // Save first to get the Order Id
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Ensure OrderId is set on all items
            foreach (var item in order.Items)
            {
                item.OrderId = order.Id;
            }
            await _context.SaveChangesAsync();

            // Now write the audit log with the real Id
            order.AuditLog = $"{fullName} created Order #{order.Id} at {order.CreatedAt.ToLocalTime():hh:mm tt} on {order.CreatedAt.ToLocalTime():MMM d, yyyy}";
            await _context.SaveChangesAsync();

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = order.AuditLog;
            return RedirectToAction(nameof(Detail), new { id = order.Id });
        }

        // GET: /Order/Detail/{id}
        public async Task<IActionResult> Detail(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
                return NotFound();

            // Regular users can only see their own orders
            if (!isAdminOrManager && order.UserId != userId)
                return Forbid();

            return View(order);
        }
    }
}
