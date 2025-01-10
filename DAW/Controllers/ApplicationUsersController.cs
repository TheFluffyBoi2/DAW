﻿using DAW.Data;
using DAW.Data.Migrations;
using DAW.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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
            ViewBag.Message = TempData["message"];
            ViewBag.Alert = TempData["messageType"];
            AlreadySent(id);
            AlreadyFriends(id);
            ViewBag.HasAccess = HasAccess(user);

            // daca profilul e privat, doar cei cu acces pot vedea
            if (!user.IsPublic)
                {
                    if (HasAccess(user) || ViewBag.AlreadyFriends == true)
                    {
                        return View(user);
                    }
                    return Forbid();
                }
            return View(user);
        }

        [Authorize(Roles = "Admin, User")]
        [HttpPost]
        public async Task<IActionResult> Show([FromForm] Post post, IFormFile image)
        {

            post.Date = DateTime.Now;
            post.UserId = _userManager.GetUserId(User);
            post.Likes = 0;
            post.Dislikes = 0;

            if (image != null && image.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif",
".mp4", ".mov" };
                var fileExtension = Path.GetExtension(image.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("Image", "The file must be an image (jpg, jpeg, png, gif) or a video (mp4, mov).");
                    TempData["message"] = ModelState.Values
                                     .SelectMany(v => v.Errors)
                                     .Select(e => e.ErrorMessage)
                                     .FirstOrDefault();
                    TempData["messageType"] = "alert-danger";
                    return RedirectToAction("Show", "ApplicationUsers", post.UserId);
                }

                var storagePath = Path.Combine(_env.WebRootPath, "images",
                image.FileName);
                var databaseFileName = "/images/" + image.FileName;

                using (var fileStream = new FileStream(storagePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                ModelState.Remove(nameof(post.Image));
                post.Image = databaseFileName;
            }
            else
            {
                ModelState.Remove("Image");
            }

            if (post.Video != null && post.Video.Length > 0)
            {
                if (ExtractVideoId(post.Video) == string.Empty)
                {
                    ModelState.AddModelError("Video", "The URL must be from YouTube.");
                    TempData["message"] = ModelState.Values
                                     .SelectMany(v => v.Errors)
                                     .Select(e => e.ErrorMessage)
                                     .FirstOrDefault();
                    TempData["messageType"] = "alert-danger";
                    return RedirectToAction("Show", "ApplicationUsers", post.UserId);
                }
                post.Video = "https://youtube.com/embed/"+ExtractVideoId(post.Video);
            }
            else
            {
                ModelState.Remove("Video");
            }


            if (ModelState.IsValid)
            {
                db.Posts.Add(post);
                db.SaveChanges();
                TempData["message"] = "Post added";
                TempData["messageType"] = "alert-success";
                return RedirectToAction("Show", "ApplicationUsers", post.UserId);
            }
            else
            {
                TempData["message"] = ModelState.Values
                                 .SelectMany(v => v.Errors)
                                 .Select(e => e.ErrorMessage)
                                 .FirstOrDefault();
                TempData["messageType"] = "alert-danger";
                return RedirectToAction("Show", "ApplicationUsers", post.UserId);
            }
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

        [Authorize(Roles = "Admin, User")]
        [HttpPost]
        public IActionResult Add(string id)
        {
            FriendRequest rq = new();
            rq.Date = DateTime.Now;
            rq.UserIdSender = _userManager.GetUserId(User);
            rq.UserIdReceiver = id;
            db.FriendRequests.Add(rq);
            db.SaveChanges();
            TempData["message"] = "Request sent";
            TempData["messageType"] = "alert-success";
            ViewBag.Clicked = true;
            return RedirectToAction("Show", "ApplicationUsers", new { id = rq.UserIdReceiver });
        }

        [Authorize(Roles = "Admin, User")]
        public IActionResult Friends()
        {
            ViewBag.Friends = db.UserRelationships.Where(r => (r.UserId1 == _userManager.GetUserId(User)
                                                                                    || r.UserId2 == _userManager.GetUserId(User))
                                                                                    && r.Relation == "Friends").Include(r => r.User1).Include(r => r.User2);
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            return View();
        }

        [NonAction]
        public bool HasAccess(ApplicationUser user)
        {
            return user.Id == _userManager.GetUserId(User) || User.IsInRole("Admin");
        }

        [NonAction]
        public void AlreadySent(string id)
        {
            FriendRequest? rq = db.FriendRequests.Where(r => r.UserIdSender == _userManager.GetUserId(User) && r.UserIdReceiver == id).FirstOrDefault();
            if (rq != null)
            {
                ViewBag.Clicked = true;
            }
        }

        [NonAction]
        public void AlreadyFriends(string id)
        {
            UserRelationships? ur = db.UserRelationships.Where(r => (r.UserId1 == id && r.UserId2 == _userManager.GetUserId(User))
                                                                    || (r.UserId2 == id && r.UserId1 == _userManager.GetUserId(User))
                                                                    && r.Relation == "Friends")
                                                                    .FirstOrDefault();
            if (ur != null)
            {
                ViewBag.AlreadyFriends = true;
            }
        }

        [NonAction]
        public string ExtractVideoId(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
                return string.Empty;

            if (videoUrl.Contains("youtu.be/"))
            {
                int startIndex = videoUrl.IndexOf("youtu.be/") + "youtu.be/".Length;
                return videoUrl.Substring(startIndex, 11); // Extract 11-character ID
            }

            if (videoUrl.Contains("v="))
            {
                int startIndex = videoUrl.IndexOf("v=") + "v=".Length;
                string id = videoUrl.Substring(startIndex);
                int ampersandIndex = id.IndexOf("&");
                if (ampersandIndex > -1)
                {
                    id = id.Substring(0, ampersandIndex); // Remove extra parameters
                }
                return id;
            }

            return string.Empty;
        }
    }
}
