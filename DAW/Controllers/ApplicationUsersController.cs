﻿using DAW.Data;
using DAW.Data.Migrations;
using DAW.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using System.Security.Claims;

namespace DAW.Controllers
{
    public class ApplicationUsersController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        public ApplicationUsersController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            db = context;
            _env = env;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [Authorize(Roles = "Admin, User")]
        public IActionResult Index()
        {
            ViewBag.Users = db.Users.Include("Posts");
            return View();
        }

        [Authorize(Roles = "Admin, User")]
        public IActionResult Show(string id)
        {
            ApplicationUser user = db.Users.Include("Posts")
              .Where(_user => _user.Id == id).First();

            ViewBag.HasAccess = HasAccess(user);

            // daca profilul e privat, doar cei cu acces pot vedea
            if (!user.IsPublic)
                {
                    if (HasAccess(user))
                    {
                        return View(user);
                    }
                    return Forbid();
                }
            return View(user);
        }

        [Authorize(Roles = "Admin, User")]
        public IActionResult Edit(string id)
        {
            ApplicationUser user = db.Users.Include("Posts")
                .Where(_user => _user.Id == id).First();

            if (HasAccess(user))
            {
                return View(user);
            }
            else
            {
                return RedirectToAction("Show", "ApplicationUsers", new { id = user.Id });
            }
        }

        [Authorize(Roles = "Admin, User")]
        [HttpPost]
        public async Task<IActionResult> Edit(string id, ApplicationUser requestUser, IFormFile profilePicture)
        {
            ApplicationUser user = db.Users.Find(id);
            if (profilePicture != null && profilePicture.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif",
".mp4", ".mov" };
                var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ProfilePicture", "The file must be an image (jpg, jpeg, png, gif) or a video (mp4, mov).");
                    return View(requestUser);
                }

                var storagePath = Path.Combine(_env.WebRootPath, "images",
                profilePicture.FileName);
                var databaseFileName = "/images/" + profilePicture.FileName;

                using (var fileStream = new FileStream(storagePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(fileStream);
                }

                ModelState.Remove(nameof(user.ProfilePicture));
                user.ProfilePicture = databaseFileName;
            }
            else
            {
                ModelState.Remove("ProfilePicture");
            }

            user.FirstName = requestUser.FirstName;
            user.LastName = requestUser.LastName;
            user.Bio = requestUser.Bio;
            user.IsPublic = requestUser.IsPublic;

            if (TryValidateModel(user))
            {
                db.SaveChanges();
                return RedirectToAction("Show", "ApplicationUsers", new { id = user.Id });
            }
            else
            {
                return View(requestUser);
            }
        }

        [NonAction]
        public bool HasAccess(ApplicationUser user)
        {
            return user.Id == _userManager.GetUserId(User) || User.IsInRole("Admin");
        }
    }
}
