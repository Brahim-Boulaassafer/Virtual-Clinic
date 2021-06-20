﻿using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;
using VirtualClinic.Data;
using VirtualClinic.ViewModels;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using VirtualClinic.Services.IdentityService;
using VirtualClinic.Services.EmailService;
using System.IO;
using MimeKit;
using Microsoft.AspNetCore.Hosting;
using VirtualClinic.Services.Upload;
using VirtualClinic.Models.Identity;
using System.Linq;

namespace VirtualClinic.Services.IdentityService
{
    public class UserTaskService : IUserTaskService<ApplicationUserViewModel>
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IMailService _mailService;
        public UserTaskService(IMailService mailService, ApplicationDbContext context, IWebHostEnvironment environment, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _db = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _mailService = mailService;
            _environment = environment;
        }

        public UserTaskService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _db = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // this Method create a user(patient or doctor) and add it to database
        public async Task<IdentityResult> CreateUser(RegisterViewModel register, string AsDoctor)
        {
            if (AsDoctor.ToLower() == "true")
                register.IsDoctor = true;
            else
                register.IsDoctor = false;
            var Upload = new UploadFile(_environment);
            ApplicationUser user = new ApplicationUser();
            byte[] Image = null;
            if (register.Image != null)
            {
                // Upload its an action on Upload Service that that convert file .png /... to Array of byte
                Image = Upload.Upload(register.Image);
            }
            else
            {
                // here we take image from wwwroot as default if the user thasn't upload any image.
                var path = _environment.WebRootFileProvider.GetFileInfo("Images/Img/NoImage.jpg")?.PhysicalPath;
                Image = File.ReadAllBytes(path);
            }
             
            Random R = new Random();
            string roleid = "";
            if (register.IsDoctor)
            {
                user = new Models.Identity.Doctor {Image=Image, State = register.State, FirstName = register.FirstName, LastName = register.LastName, Birthday = register.BirthDate, UserName = register.LastName.ToUpper() + R.Next(0, 9999), Email = register.Email, City = register.City, Country = register.Country, Gender = register.Gender, IdCard = register.IdCard, PhoneNumber = register.PhoneNumber, Speciality = register.Specialist, Price = register.Price, Adress = register.Adress, IsDoctor = true };
                roleid = "2301D884-221A-4E7D-B509-0113DCC044E2";
            }
            else
            {
                user = new Patient {Image=Image, State = register.State, Adress = register.Adress,FirstName = register.FirstName, LastName = register.LastName, Birthday = register.BirthDate, UserName = register.LastName.ToUpper() +R.Next(0, 9999), Email = register.Email, City = register.City, Country = register.Country, Gender = register.Gender, IdCard = register.IdCard, PhoneNumber = register.PhoneNumber };
                roleid = "2301D884-221A-4E7D-B509-0113DCC045E3";
            }
            var result = await _userManager.CreateAsync(user, register.Password);
            AddUserRole(user.Id, roleid);
            await _db.SaveChangesAsync();
            return result;
        }
        // LoginUser Method : it's an action that look if user have authorisation to use the we app. 
        public async Task<SignInResult> LoginUser(LoginViewModel login, bool RememberMe, bool lockoutOnFailure)
        {
            var user = await _userManager.FindByEmailAsync(login.Email);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName, login.Password, login.RememberMe, lockoutOnFailure: false);

                return result;
            }
            return null;
        }
        // we use this Method to send confirmation email to users 
        public void SendConfirmationEmail(ApplicationUser user)
        {
            // path of temlate that send to users
            string FilePath = _environment.WebRootFileProvider.GetFileInfo("Templates/HtmlMailConfirmationPageModel.html")?.PhysicalPath; ;
            StreamReader str = new StreamReader(FilePath);
            string MailText = str.ReadToEnd();
            str.Close();
            string Body = $"<a  class=\"link-white\" style = \"color: #ffffff; text-decoration: none;\" href =\"https://localhost:44348/Account/ConfirmEmail?Email={user.Email}\">Confirm</a>";
            MailText = MailText.Replace("[username]", user.UserName).Replace("[email]", user.Email).Replace("[link]", Body);
            MailRequest mailRequest = new MailRequest();
            mailRequest.ToEmail = user.Email;
            mailRequest.UserName = user.UserName;
            mailRequest.Subject = $"Please Confirm Your Email";
            var builder = new BodyBuilder();
            builder.HtmlBody = MailText;
            mailRequest.Body = builder.ToMessageBody();
            _mailService.SendEmailAsync(mailRequest);
        }        
        public async void Logout()
        {
            await _signInManager.SignOutAsync();
        }        
        public async void AddUserRole(string userId ,string RoleId)
        {
            var userrole = new IdentityUserRole<string>();
            userrole.RoleId = RoleId;
            userrole.UserId = userId;
            await _db.UserRoles.AddAsync(userrole);  
        }

        // using this method to get all users 
        public IQueryable<ApplicationUserViewModel> GetAll()
        {

            IQueryable<ApplicationUser> Users = _db.AppUsers.Select(User => new ApplicationUser
            {
                IsDoctor = User.IsDoctor,
                Image = User.Image,
                FirstName = User.FirstName,
                LastName = User.LastName,
                Birthday = User.Birthday,
                IdCard = User.IdCard,
                Country = User.Country,
                State = User.State,
                City = User.City,
                Adress = User.Adress,
                Gender = User.Gender,
                Email = User.Email,

            });


            var UsersViewModel = ModelsToViewModels(Users);
            return UsersViewModel;
        }
        // using this method to get users by their id
        public async Task<ApplicationUserViewModel> GetUserById(string id)
        {
            var User = await _userManager.FindByIdAsync(id);
            var userModel = ModelToViewModel(User);
            return userModel;
        }
        // ...... by their email
        public async Task<ApplicationUserViewModel> GetUserByEmail(string email)
        {
            var User = await _userManager.FindByEmailAsync(email);
            var userModel = ModelToViewModel(User);
            return userModel;
        }
        // .... by their user name
        public async Task<ApplicationUserViewModel> GetUserByUserName(string userName)
        {
            var User = await _userManager.FindByNameAsync(userName);
            var userModel = ModelToViewModel(User);
            return userModel;
        }
        // using this method to convert all users ApplicationUser class to ApplicationUserViewModel (model to view model)
        public IQueryable<ApplicationUserViewModel> ModelsToViewModels(IQueryable<ApplicationUser> Users)
        {
            IQueryable<ApplicationUserViewModel> UsersViewModel = Users.Select(UserViewModel => new ApplicationUserViewModel()
            {
                IsDoctor = UserViewModel.IsDoctor,
                Image = UserViewModel.Image,
                FirstName = UserViewModel.FirstName,
                LastName = UserViewModel.LastName,
                Birthday = UserViewModel.Birthday,
                IdCard = UserViewModel.IdCard,
                Country = UserViewModel.Country,
                State = UserViewModel.State,
                City = UserViewModel.City,
                Adress = UserViewModel.Adress,
                Gender = UserViewModel.Gender,
                Email = UserViewModel.Email
            });
            return UsersViewModel;
        }
        // using this method to convert ApplicationUser class to ApplicationUserViewModel (model to view model)
        public ApplicationUserViewModel ModelToViewModel(ApplicationUser User)
        {
            ApplicationUserViewModel UsersViewModel = new ApplicationUserViewModel(_environment)
            {
                IsDoctor = User.IsDoctor,
                Image = User.Image,
                FirstName = User.FirstName,
                LastName = User.LastName,
                Birthday = User.Birthday,
                IdCard = User.IdCard,
                Country = User.Country,
                State = User.State,
                City = User.City,
                Adress = User.Adress,
                Gender = User.Gender,
                Email = User.Email
            };
            return UsersViewModel; throw new NotImplementedException();
        }


    }




}

