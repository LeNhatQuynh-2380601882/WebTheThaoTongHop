using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Controllers
{
    public class AuthController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET /Auth/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            ViewData["Title"] = "Đăng nhập";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST /Auth/Login
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false, string? returnUrl = null)
        {
            ViewData["Title"] = "Đăng nhập";
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập email và mật khẩu.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
                return View();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ hỗ trợ.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLogin = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Thử lại sau 15 phút.");
                return View();
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View();
        }

        // GET /Auth/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            ViewData["Title"] = "Đăng ký tài khoản";
            return View();
        }

        // POST /Auth/Register
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword)
        {
            ViewData["Title"] = "Đăng ký tài khoản";

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError("", "Vui lòng điền đầy đủ thông tin.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View();
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError("", "Email này đã được sử dụng. Vui lòng chọn email khác.");
                return View();
            }

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true // Bỏ qua xác nhận email trong demo
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Customer");
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["Success"] = $"🎉 Chào mừng {fullName}! Tài khoản đã được tạo thành công.";
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", TranslateIdentityError(error.Code, error.Description));

            return View();
        }

        // POST /Auth/Logout
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET /Auth/AccessDenied
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = "Không có quyền truy cập";
            return View();
        }

        private static string TranslateIdentityError(string code, string description) => code switch
        {
            "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
            "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất 1 chữ số.",
            "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất 1 chữ thường.",
            "PasswordRequiresUpper" => "Mật khẩu phải chứa ít nhất 1 chữ hoa.",
            "DuplicateUserName" => "Email này đã được sử dụng.",
            "DuplicateEmail" => "Email này đã được sử dụng.",
            _ => description
        };
    }
}
