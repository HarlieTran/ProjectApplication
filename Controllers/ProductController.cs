using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApplication.Data;
using ProjectApplication.Models;

namespace ProjectApplication.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Product - visible to everyone logged in
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Where(p => p.IsAvailable)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        // GET: /Product/Manage - Admin/Manager only
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Manage()
        {
            var products = await _context.Products
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        // GET: /Product/Create
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Create(Product model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = $"Product '{model.Name}' created successfully.";
            return RedirectToAction(nameof(Manage));
        }

        // GET: /Product/Edit/{id}
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product is null)
                return NotFound();

            return View(product);
        }

        // POST: /Product/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Edit(Product model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.Products.Update(model);
            await _context.SaveChangesAsync();

            TempData["StatusType"] = "success";
            TempData["StatusMessage"] = $"Product '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Manage));
        }

        // POST: /Product/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product is null)
                return NotFound();

            product.IsAvailable = false; // soft delete
            await _context.SaveChangesAsync();

            TempData["StatusType"] = "warning";
            TempData["StatusMessage"] = $"Product '{product.Name}' has been removed.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
