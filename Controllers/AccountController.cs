using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SampleSecureWeb.Data;
using SampleSecureWeb.Models;
using SampleSecureWeb.ViewModels;

namespace SampleSecureWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUser _userData;

        public AccountController(IUser userData)
        {
            _userData = userData;
        }

        // GET: AccountController
        public ActionResult Index()
        {
            var users = _userData.GetUsers();
            return View(users);
        }

        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegistrationViewModel registrationViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (!IsValidPassword(registrationViewModel.Password))
                    {
                        // Menambahkan pesan kesalahan pada ModelState
                        ModelState.AddModelError(
                            "Password",
                            "Password harus minimal 12 karakter dan mengandung huruf besar, huruf kecil, dan angka."
                        );
                        return View(registrationViewModel); // Kembali ke view jika password tidak valid
                    }

                    var user = new User
                    {
                        Username = registrationViewModel.Username,
                        Password = registrationViewModel.Password,
                        RoleName = "contributor"
                    };

                    _userData.Registration(user);
                    return RedirectToAction("Index");
                }
            }
            catch (System.Exception ex)
            {
                ViewBag.Error = ex.Message;
            }

            return View(registrationViewModel);
        }

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Login(LoginViewModel loginViewModel)
        {
            try
            {
                var user = new User
                {
                    Username = loginViewModel.Username,
                    Password = loginViewModel.Password
                };

                var loginUser = _userData.Login(user);
                if (loginUser == null)
                {
                    ViewBag.Message = "Invalid login attempt";
                    return View(loginViewModel);
                }

                var claims = new List<Claim> { new Claim(ClaimTypes.Name, user.Username) };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = loginViewModel.RememberLogin }
                );
                return RedirectToAction("Index", "Home");
            }
            catch (System.Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View(loginViewModel);
        }

        // 2.1.5 – Allow Users to Change Password
        public ActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _userData.GetUserByUsername(model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found");
                return View(model);
            }

            // Verify old password
            if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, user.Password))
            {
                ModelState.AddModelError("", "Old password is incorrect");
                return View(model);
            }

            // Update password
            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _userData.UpdatePassword(user);

            ViewBag.Message = "Password changed successfully. Please login again.";
            ViewBag.ShowLoginButton = true; // Tampilkan tombol login setelah password berhasil diubah

            return View();
        }

        private bool IsValidPassword(string password)
        {
            //2.1.3 – No Password Truncation 
            string normalizedPassword = Regex.Replace(password, @"\s{2,}", " ");

            // Password harus minimal 12 karakter, mengandung huruf besar, huruf kecil, dan angka
            //2.1.1 – Minimum Password Length
            //2.1.2 – Maximum Password Length
            //2.1.4 – Allow Any Printable Unicode Character
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[\P{C}].{12,128}$");
            return regex.IsMatch(password);
        }

        //2.1.7 – Check Against Breached Passwords
        public bool IsPasswordBreached(string password)
        {
            var breachedPasswords = new List<string> { "123456", "password", "123456789" }; 
            return breachedPasswords.Contains(password);
        }
    }
}
