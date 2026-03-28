# Section 1: Authentication & Authorization

## Overview
This section implements a secure user authentication and authorization system
using ASP.NET Core Identity. It covers user registration, login, logout,
role-based access control, and account lockout protection.

---

## Tech Stack
- ASP.NET Core MVC (.NET 8)
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- Bootstrap 5

---

## Setup Instructions

### Prerequisites
- Visual Studio 2022
- SQL Server (local)
- .NET 8 SDK

### Steps
1. Clone the repository
   git clone <your-github-link>

2. Open the solution in Visual Studio
   ProjectApplication.sln

3. Update the connection string in appsettings.json
   "DefaultConnection": "Data Source=localhost;Initial Catalog=ProjectAppDb;
   Integrated Security=True;Encrypt=True;Trust Server Certificate=True"

4. Run database migrations in Package Manager Console
   Update-Database

5. Run the application
   Press F5 in Visual Studio

6. Seed data runs automatically on startup
   - All 3 roles are created (Admin, Manager, User)
   - 3 default accounts are created (see Test Accounts below)

---

## Test Accounts

| Role    | Email                | Password     |
|---------|----------------------|--------------|
| Admin   | admin@site.com       | Admin@1234   |
| Manager | manager@site.com     | Manager@1234 |
| User    | user@site.com        | User@1234    |

---

## Features Implemented

### User Registration
- Fields: First Name, Last Name, Email, Password, Confirm Password
- Full server-side validation on all fields
- Password must be at least 8 characters with uppercase and digit
- Duplicate email is rejected
- Password hashed automatically via Identity (no manual hashing)
- New users are auto-assigned the User role on registration
- Anti-forgery token enforced on POST

### Login
- Login by email and password
- Remember Me — persistent cookie across browser sessions when checked,
  session cookie when unchecked
- Anti-forgery token enforced on POST
- POST only works after GET (enforced via anti-forgery cookie)
- No manual password comparison — PasswordSignInAsync used exclusively
- Invalid credentials show a generic error message

### Logout
- POST only (not a plain link) to prevent CSRF-based logout attacks
- Requires authentication via [Authorize]
- Anti-forgery token enforced
- Redirects to Login page after logout

### Account Lockout
- Triggers after 5 consecutive failed login attempts
- Account locked for 5 minutes
- Locked users are redirected to a dedicated Lockout page
- Lockout applies to all users including newly registered ones

### Password Security
- All passwords hashed via ASP.NET Core Identity default hasher
- No manual hashing or comparison anywhere in the codebase
- Password policy: min 8 characters, requires uppercase, requires digit

### Role-Based Access Control
- 3 roles: Admin, Manager, User
- [Authorize(Roles="...")] enforced on protected controllers
- Navbar menus differ per role:
  - Admin: sees Admin Dashboard + Manager Panel
  - Manager: sees Manager Panel only
  - User: no dashboard links
- Unauthenticated users visiting protected routes
  are redirected to Login
- Authenticated users without required role
  are redirected to AccessDenied (403)

### Anti-Forgery Protection
- All POST forms include anti-forgery tokens via Tag Helpers
- [ValidateAntiForgeryToken] on all POST actions
- Direct POST requests without a prior GET are rejected

---

## Project Structure (Section 1)

Controllers/
  AccountController.cs   — Register, Login, Logout, Lockout, AccessDenied
  AdminController.cs     — Protected route (Admin role only)

Models/
  ApplicationUser.cs     — Extends IdentityUser with FirstName, LastName

ViewModels/
  LoginViewModel.cs      — Email, Password, RememberMe
  RegisterViewModel.cs   — FirstName, LastName, Email, Password, ConfirmPassword

Views/Account/
  Login.cshtml           — Login form with Remember Me
  Register.cshtml        — Registration form
  Lockout.cshtml         — Account locked page
  AccessDenied.cshtml    — 403 page with role-aware messaging

Views/Shared/
  _Layout.cshtml         — Navbar with role-based menus, displays full name

Data/
  AppDbContext.cs        — IdentityDbContext
  SeedData.cs            — Seeds roles and default accounts on startup

Program.cs               — Identity config, lockout policy, cookie settings

---

## Security Notes
- No plain text passwords stored anywhere
- No manual password comparison
- Authentication cookie created only after successful login
  via PasswordSignInAsync
- LocalRedirect used to prevent open redirect attacks
- Anti-forgery tokens prevent CSRF on all POST actions
- Lockout policy prevents brute force attacks