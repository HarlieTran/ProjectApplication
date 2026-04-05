# Group Project Part 2: User Account System

## Overview
This project implements a secure user authentication and authorization system
using ASP.NET Core Identity. It covers user registration, login, logout,
role-based access control, and account lockout protection. An Admin dashboard for user management,
role management system, and a limited manager panel is included.

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

### 1. User Authentication

#### User Registration
- Fields: First Name, Last Name, Email, Password, Confirm Password
- Full server-side validation on all fields
- Password must be at least 8 characters with uppercase and digit
- Duplicate email is rejected
- Password hashed automatically via Identity (no manual hashing)
- New users are auto-assigned the **User** role on registration
- Anti-forgery token enforced on POST

#### Login
- Login by email and password
- **Remember Me** — persistent cookie across browser sessions when checked,
  session cookie when unchecked
- Anti-forgery token enforced on POST
- POST only works after GET (enforced via anti-forgery cookie)
- No manual password comparison — `PasswordSignInAsync` used exclusively
- Invalid credentials show a generic error message

#### Logout
- POST only (not a plain link) to prevent CSRF-based logout attacks
- Requires authentication via `[Authorize]`
- Anti-forgery token enforced
- Redirects to Login page after logout

#### Account Lockout
- Triggers after 5 consecutive failed login attempts
- Account locked for 5 minutes
- Locked users are redirected to a dedicated Lockout page
- Lockout applies to all users including newly registered ones

#### Password Security
- All passwords hashed via ASP.NET Core Identity default hasher
- No manual hashing or comparison anywhere in the codebase
- Password policy: min 8 characters, requires uppercase, requires digit

---

### 2. Role-Based Access Control

#### Roles
The system includes 3 roles with different access levels:

| Role    | Permissions |
|---------|-------------|
| **Admin** | Full control: Create/edit/delete users, manage roles, assign/remove roles, lock/unlock accounts, reset passwords |
| **Manager** | Limited access: View-only dashboard showing all users and statistics (cannot modify) |
| **User** | Standard application access (no admin features) |

#### Authorization
- `[Authorize(Roles="...")]` enforced on protected controllers
- Navbar menus differ per role:
  - **Admin**: sees Admin Dashboard + Role Management + Manager Panel
  - **Manager**: sees Manager Panel only
  - **User**: no dashboard links
- Unauthenticated users visiting protected routes are redirected to Login
- Authenticated users without required role are redirected to AccessDenied (403)

---

### 3. Admin Dashboard - User Management

Admins have full control over user accounts through a comprehensive dashboard.

#### Features:
- **View All Users** — Display all registered users with their roles and status
- **Create User** — Add new users with email, password, and role assignment
- **Edit User** — Update user information:
  - Change email
  - Change first/last name
  - Change roles (dropdown selection)
- **Delete User** — Remove users from the system
- **Lock/Unlock User** — Lock accounts to prevent login, or unlock to restore access
- **Reset Password** — Generate temporary passwords for users
- **User Statistics** — Dashboard shows total users, admins, managers, standard users, and locked accounts

#### Security:
- All actions require `[Authorize(Roles="Admin")]`
- Anti-forgery tokens on all POST operations
- Cannot delete or lock the currently logged-in admin account (prevents self-lockout)

---

### 4. Admin Dashboard - Role Management

Admins can manage all aspects of roles and role assignments.

#### Features:
- **View All Roles** — Display all roles with user counts and assigned users
- **Create Role** — Add new roles to the system
  - Validates role name is unique
  - Auto-redirects to role list on success
- **Edit Role** — Modify role names
  - Shows all users currently assigned to the role
  - Updates role name across the system
- **Delete Role** — Remove roles from the system
  - **Protection**: Cannot delete a role if users are assigned to it
  - Shows error message with user count if deletion fails
- **Assign Role to User** — Assign roles to users via dropdowns
  - Dropdown of all users (showing username and email)
  - Dropdown of all available roles
  - Prevents duplicate role assignments
- **Remove Role from User** — Remove specific roles from users
  - Available directly on the Roles page
  - Shows all users under each role with "Remove" button
  - Requires confirmation before removal

#### UI/UX:
- Card-based layout showing roles grouped by category
- Badge indicators for user counts
- Color-coded role badges (Admin=red, Manager=yellow, User=gray)
- Success/error messages via TempData alerts
- Confirmation dialogs for destructive actions (delete role, remove user from role)

---

### 5. Manager Panel

Managers have a limited dashboard for viewing user information without modification privileges.

#### Features:
- **View Users** — Read-only table displaying:
  - Full name
  - Email address
  - Role assignment
  - Account status (Active/Locked)
- **Statistics Cards** — Dashboard overview showing:
  - Total users
  - Number of admins
  - Number of managers
  - Number of standard users
  - Number of locked accounts
- **No Edit Access** — Managers cannot:
  - Create, edit, or delete users
  - Manage roles
  - Lock/unlock accounts
  - Reset passwords

#### Access Control:
- Requires `[Authorize(Roles="Manager")]`
- Separate controller (`ManagerController`) from admin functions
- Info banner reminds managers they have read-only access

---

### 6. Anti-Forgery Protection
- All POST forms include anti-forgery tokens via Tag Helpers (`@Html.AntiForgeryToken()`)
- `[ValidateAntiForgeryToken]` on all POST actions
- Direct POST requests without a prior GET are rejected

---

## Project Structure
```
ProjectApplication/
│
├── Controllers/
│   ├── AccountController.cs       # Register, Login, Logout, Lockout, AccessDenied
│   ├── AdminController.cs         # User management + Role management
│   ├── ManagerController.cs       # Manager panel (view-only dashboard)
│   └── HomeController.cs          # Public home page
│
├── Models/
│   ├── ApplicationUser.cs         # Extends IdentityUser (FirstName, LastName)
│   └── ErrorViewModel.cs          # Error page model
│
├── ViewModels/
│   ├── LoginViewModel.cs          # Email, Password, RememberMe
│   ├── RegisterViewModel.cs       # FirstName, LastName, Email, Password, ConfirmPassword
│   ├── AdminDashboardViewModel.cs # Dashboard statistics and user list
│   ├── AdminUserListItemViewModel.cs # Individual user display data
│   ├── CreateUserViewModel.cs     # Create user form
│   ├── EditUserViewModel.cs       # Edit user form
│   ├── ResetPasswordViewModel.cs  # Reset password form
│   ├── RoleViewModel.cs           # Role display with user list
│   ├── CreateRoleViewModel.cs     # Create role form
│   ├── EditRoleViewModel.cs       # Edit role form
│   ├── AssignRoleViewModel.cs     # Assign role to user form
│   └── UserRoleViewModel.cs       # User's roles display
│
├── Views/
│   ├── Account/
│   │   ├── Login.cshtml           # Login form with Remember Me
│   │   ├── Register.cshtml        # Registration form
│   │   ├── Lockout.cshtml         # Account locked page
│   │   └── AccessDenied.cshtml    # 403 page with role-aware messaging
│   │
│   ├── Admin/
│   │   ├── Index.cshtml           # User management dashboard
│   │   ├── Create.cshtml          # Create user form
│   │   ├── Edit.cshtml            # Edit user form
│   │   ├── ResetPassword.cshtml   # Reset password form
│   │   ├── Roles.cshtml           # Role management dashboard
│   │   ├── CreateRole.cshtml      # Create role form
│   │   ├── EditRole.cshtml        # Edit role form
│   │   └── AssignRole.cshtml      # Assign role to user form
│   │
│   ├── Manager/
│   │   └── Index.cshtml           # Manager panel (view-only user dashboard)
│   │
│   ├── Home/
│   │   └── Index.cshtml           # Home page
│
├── Data/
│   ├── AppDbContext.cs            # IdentityDbContext with ApplicationUser
│   └── SeedData.cs                # Seeds roles and default test accounts
│
├── Migrations/                    # EF Core database migrations
│
├── wwwroot/                       # Static files (CSS, JS, images)
│
├── appsettings.json               # Configuration (connection strings, logging)
├── Program.cs                     # App configuration, DI, Identity setup
└── ProjectApplication.csproj      # Project file
```

---

## Security Notes
- No plain text passwords stored anywhere
- No manual password comparison
- Authentication cookie created only after successful login
  via PasswordSignInAsync
- LocalRedirect used to prevent open redirect attacks
- Anti-forgery tokens prevent CSRF on all POST actions
- Lockout policy prevents brute force attacks

---

## Team Contributions

### Section 1: Authentication System
- **Implemented by:** Nguyen Hai Anh Tran
- Features: User registration, login, logout, account lockout, password security

### Section 2: User Management
- **Implemented by:** Bowale Omode
- Features: Admin dashboard for creating, editing, deleting, locking users

### Section 3: Role Management & Manager Panel
- **Implemented by:** Peter Do
- Features:
  - Complete role management system (create, edit, delete roles)
  - Assign/remove roles from users
  - Manager panel with read-only user dashboard
  - Enhanced Roles page with user lists and remove functionality

---

## License
This project is for educational purposes as part of PROG8555 coursework.

---

## Contact
**Team Members:**
- Nguyen Hai Anh Tran (9013769) - Authentication System
- Bowale Omode (9024729) - User Management Dashboard  
- Peter Do (9086580) - Role Management & Manager Panel

**Course:** PROG8555 - Microsoft Web Technologies  
**Institution:** Conestoga College
