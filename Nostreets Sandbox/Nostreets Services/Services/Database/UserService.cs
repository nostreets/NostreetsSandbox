﻿using Nostreets_Services.Domain;
using Nostreets_Services.Domain.Base;
using Nostreets_Services.Enums;
using Nostreets_Services.Interfaces.Services;
using Nostreets_Services.Services.Email;
using NostreetsExtensions;
using NostreetsExtensions.Interfaces;
using NostreetsExtensions.Utilities;
using NostreetsORM;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;

namespace Nostreets_Services.Services.Database
{
    public class UserService : IUserService
    {
        public UserService(IEmailService emailSrv, IDBService<User, string> userDBSrv, IDBService<Token, string> tokenDBSrv)
        {
            _emailSrv = emailSrv;
            _userDBService = userDBSrv;
            _tokenDBService = tokenDBSrv;
        }

        private IEmailService _emailSrv = null;
        private IDBService<User, string> _userDBService = null;
        private IDBService<Token, string> _tokenDBService = null;

        public string RequestIp => HttpContext.Current.GetIPAddress();
        public User SessionUser
        {
            get
            {
                string userId = CacheManager.GetItem<string>(RequestIp);

                if (userId != null)
                    if (CacheManager.Contains(userId))
                        _sessionUser = CacheManager.GetItem<User>(userId);
                    else
                        _sessionUser = _userDBService.Get(CacheManager.GetItem<string>(RequestIp));

                if (_sessionUser != null && !CacheManager.Contains(_sessionUser.Id))
                    CacheManager.InsertItem(_sessionUser.Id, _sessionUser, DateTimeOffset.Now.AddHours(2));
                return _sessionUser;

            }
        }

        User _sessionUser = null;

        public bool CheckIfUserCanLogIn(string username, string password, out string failureReason)
        {
            failureReason = null;
            User user = SessionUser ?? GetByUsername(username);
            bool result = false,
                 hasIP = (user == null) ? false : user.Settings.IPAddresses.Contains(RequestIp);



            if (!hasIP)
            {
                string html = HttpContext.Current.Server.MapPath("\\assets\\UnindentifiedLogin.html").ReadFile()
                                         .FormatString(user.UserName, DateTime.Now.Timestamp());

                _emailSrv.Send("no-reply@nostreetssolutions.com"
                              , user.Contact.PrimaryEmail
                              , "Nostreets Sandbox Unindentified Login"
                              , "Nostreets Sandbox Unindentified Login"
                              , html);
            }


            if (user == null)
                failureReason = "User doesn't exist...";

            else if (!ValidatePassword(user.Password, password))
                failureReason = "Invalid password for " + username + "...";

            else if (!user.Settings.HasVaildatedEmail)
                failureReason = username + "'s email is not validated...";

            else if (user.Settings.ValidateIPBeforeLogin && !hasIP)
                failureReason = username + " does not authorize this computer for login...";

            else if (user.Settings.TwoFactorAuthEnabled)
            {
                if (user.Settings.TFAuthByPhone)
                {
                    //todo SMSService txt code to users phone
                }
                else
                {
                    //todo EmailService txt code to users email
                }

                failureReason = "2nd Code was sent to " + ((user.Settings.TFAuthByPhone) ? "Phone" : "Email");
            }
            else
                result = true;



            return result;
        }

        public void Delete(string id)
        {
            _userDBService.Delete(id);
        }

        public User GetUser(string id)
        {
            return _userDBService.Get(id);
        }

        public List<User> GetAll()
        {
            return _userDBService.GetAll();
        }

        public User GetByUsername(string username)
        {
            return FirstOrDefault(a => a.UserName == username || a.Contact.PrimaryEmail == username);
        }

        public void LogIn(string username, string password, bool rememberDevice = false)
        {
            User user = null;
            if (CheckIfUserCanLogIn(username, password, out string reason))
            {
                user = GetByUsername(username);

                if (rememberDevice && !user.Settings.IPAddresses.Contains(RequestIp))
                    user.Settings.IPAddresses.Add(RequestIp);

                CacheManager.InsertItem(RequestIp, user.Id);
                HttpContext.Current.SetCookie("loggedIn", "true");
            }
            else
                throw new Exception(reason);

        }

        public async Task<string> RegisterAsync(User user)
        {
            user.Id = Insert(user);

            Token token = new Token
            {
                ExpirationDate = DateTime.Now.AddDays(7),
                IsDeleted = false,
                UserId = user.Id,
                ModifiedUserId = user.Id,
                Name = user.UserName + "'s Registion Email Token",
                DateCreated = DateTime.Now,
                DateModified = DateTime.Now,
                Type = TokenType.EmailValidtion
            };

            token.Id = Insert(token);

            string html = HttpContext.Current.Server.MapPath("\\assets\\ValidateEmail.html").ReadFile()
                                     .Replace("{url}", HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority)
                                     + "?token={0}&userId={1}".FormatString(token.Id, user.Id));


            if (!await _emailSrv.SendAsync("no-reply@nostreetssolutions.com"
                              , user.Contact.PrimaryEmail
                              , "Nostreets Sandbox Email Validation"
                              , "Nostreets Sandbox Email Validation"
                              , html))
                throw new Exception("Email for registation not sent...");

            return user.Id;
        }

        public string ValidateToken(string tokenId, string userId)
        {
            string result = "";
            Token userToken = _tokenDBService.Get(tokenId);// FirstOrDefault(a => !a.IsDisabled && a.ExpirationDate > DateTime.Now && a.UserId == userId);

            if (!CacheManager.Contains(RequestIp))
                CacheManager.InsertItem(RequestIp, userId);

            if (userToken == null)
                result = "Token does not exist...";

            else if (userToken.ExpirationDate < DateTime.Now)
                result = "Token is expired...";

            else if (userToken.IsDeleted)
                result = "Token no longer exists...";

            else if (userToken.UserId != userId)
                result = "Token does not belong to specified user...";

            else
            {
                User user = SessionUser;
                userToken.IsDeleted = true;
                userToken.DateModified = DateTime.Now;
                userToken.ExpirationDate = DateTime.Now;

                switch (userToken.Type)
                {
                    case TokenType.EmailValidtion:
                        user.Settings.HasVaildatedEmail = true;
                        user.Settings.IsLockedOut = false;
                        result = "Email Token Validated";
                        break;

                    case TokenType.PasswordReset:
                        result = "Password Reset Token Validated";
                        break;

                    case TokenType.PhoneValidtion:
                        user.Settings.HasVaildatedPhone = true;
                        result = "Phone Token Validated";
                        break;

                    case TokenType.TwoFactorAuth:
                        result = "Two Factor Auth Validated";
                        break;

                }

                Update(user);
                Update(userToken);
            }

            return result;
        }

        public bool CheckIfUsernameExist(string username)
        {
            return _userDBService.Where(a => a.UserName == username).FirstOrDefault() != null ? true : false;
        }

        public bool CheckIfEmailExist(string email)
        {
            return _userDBService.Where(a => a.Contact.PrimaryEmail == email).FirstOrDefault() != null ? true : false;
        }

        public bool ValidatePassword(string encyptedPassword, string password)
        {
            return Encryption.SimpleDecryptWithPassword(encyptedPassword, WebConfigurationManager.AppSettings["CryptoKey"]) == password ? true : false;
        }

        public bool ChangeUserEmail(string email, string password)
        {
            bool result = false;
            User user = SessionUser;

            if (user != null && ValidatePassword(user.Password, password))
            {
                user.Contact.PrimaryEmail = email;
                _userDBService.Update(user);
            }

            return result;
        }

        public bool ChangeUserPassword(string newPassword, string oldPassword)
        {
            bool result = false;
            User user = SessionUser;

            if (user != null && ValidatePassword(user.Password, oldPassword))
            {
                user.Password = Encryption.SimpleEncryptWithPassword(newPassword, WebConfigurationManager.AppSettings["CryptoKey"]);
                _userDBService.Update(user);
            }

            return result;
        }

        public bool UpdateUserSettings(UserSettings settings)
        {
            User user = SessionUser;
            user.Settings = settings;
            _userDBService.Update(user);
            return true;
        }

        public bool UpdateUserContactInfo(Contact contact)
        {
            User user = SessionUser;
            user.Contact = contact;
            _userDBService.Update(user);
            return true;
        }

        public void LogOut()
        {
            HttpContext.Current.SetCookie("loggedIn", "false");
            CacheManager.DeleteItem(RequestIp);
        }

        public async Task<bool> ForgotPasswordEmailAsync(string username)
        {
            bool result = false;
            if (_userDBService.Where(a => a.UserName == username || a.Contact.PrimaryEmail == username) != null)
            {
                User user = _userDBService.Where(a => a.UserName == username || a.Contact.PrimaryEmail == username).FirstOrDefault();
                Token token = new Token
                {
                    ExpirationDate = DateTime.Now.AddDays(1),
                    UserId = user.Id,
                    ModifiedUserId = user.Id,
                    Value = Guid.NewGuid().ToString(),
                    Name = user.UserName + "'s Password Reset Token",
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now,
                    Type = TokenType.PasswordReset
                };

                if (await _emailSrv.SendAsync("no-reply@nostreetssolutions.com"
                              , user.Contact.PrimaryEmail
                              , "Forgot Password to Nostreets Sandbox"
                              , "Forgot Password to Nostreets Sandbox"
                              , ""))
                    result = true;
            }

            return result;
        }

        public bool ForgotPasswordValidation(string token, string userId)
        {
            bool result = false;
            Token userToken = _tokenDBService.Where(
                a => !a.IsDeleted && a.ExpirationDate > DateTime.Now && a.Type == TokenType.EmailValidtion && a.UserId == userId && a.Value.ToString() == token
            ).FirstOrDefault();

            if (userToken != null)
            {
                User user = SessionUser;
                if (!CacheManager.Contains(RequestIp))
                    CacheManager.InsertItem(RequestIp, userId);

                result = true;
                userToken.IsDeleted = true;
                userToken.DateModified = DateTime.Now;
                user.Settings.IsLockedOut = false;
                user.Settings.HasVaildatedEmail = true;

                if (user.Settings.IPAddresses == null)
                    user.Settings.IPAddresses = new List<string> { RequestIp };
                else
                    user.Settings.IPAddresses.Add(RequestIp);

                _userDBService.Update(user);
                _tokenDBService.Update(userToken);
            }

            return result;
        }

        public IEnumerable<User> Where(Func<User, bool> predicate)
        {
            return _userDBService.Where(predicate);
        }

        public IEnumerable<Token> Where(Func<Token, bool> predicate)
        {
            return _tokenDBService.Where(predicate);
        }

        public User FirstOrDefault(Func<User, bool> predicate)
        {
            return _userDBService.FirstOrDefault(predicate);
        }

        public Token FirstOrDefault(Func<Token, bool> predicate)
        {
            return _tokenDBService.FirstOrDefault(predicate);
        }

        public void Update(User user)
        {
            if (!CacheManager.Contains(RequestIp))
                CacheManager.InsertItem(RequestIp, user.Id);

            CacheManager.InsertItem(user.Id, user);

            _userDBService.Update(user);
        }

        public void Update(Token token)
        {
            _tokenDBService.Update(token);
        }

        public string Insert(User user)
        {
            user.Settings = new UserSettings
            {
                IPAddresses = new List<string> { RequestIp }
            };
            user.Password = Encryption.SimpleEncryptWithPassword(user.Password, WebConfigurationManager.AppSettings["CryptoKey"]);
            user.Id = _userDBService.Insert(user);


            if (!CacheManager.Contains(RequestIp))
                CacheManager.InsertItem(RequestIp, user.Id);
            CacheManager.InsertItem(user.Id, user);

            return user.Id;
        }

        public string Insert(Token token)
        {
            token.UserId = token.UserId ?? SessionUser?.Id;
            token.ModifiedUserId = token.UserId ?? SessionUser?.Id;
            token.Id = _tokenDBService.Insert(token);

            return token.Id;
        }
    }

    #region Legacy
    public class UserEFService
    {
        public UserEFService()
        {
            _context = new UserDBContext("DefaultConnection");
        }

        public UserEFService(string connectionKey)
        {
            //var config = new UserMigrationConfig(); //DbMigrationsConfiguration<UserDBContext>();// { AutomaticMigrationsEnabled = true };
            //var migrator = new DbMigrator(config);
            //migrator.Update();

            _context = new UserDBContext(connectionKey);
        }

        UserDBContext _context = null;

        public bool CheckIfUserExist(string username)
        {
            if (GetByUsername(username) == default(User)) { return false; }
            else { return true; }
        }

        public void Delete(string id)
        {
            User user = _context.Users.FirstOrDefault(a => a.Id == id);

            _context.Users.Remove(user);

            if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }

        }

        public User Get(string id)
        {
            User user = _context.Users.FirstOrDefault(a => a.Id == id);
            return user;
        }

        public User GetByUsername(string username)
        {
            User user = _context.Users.FirstOrDefault(a => a.UserName == username);
            return user;
        }

        public List<User> GetAll()
        {
            return _context.Users.ToList();
        }

        public string Insert(User model)
        {
            model.Id = Guid.NewGuid().ToString();
            _context.Users.Add(model);

            if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }

            return model.Id;
        }

        public void Update(User model)
        {
            User targetedUser = _context.Users.FirstOrDefault(a => a.Id == model.Id);
            targetedUser = model;

            if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }

        }

        public IEnumerable<User> Where(Func<User, bool> predicate)
        {
            return GetAll().Where(predicate);
        }
    }
    public class UserDBContext : DbContext
    {
        public UserDBContext()
            : base("DefaultConnection" /*"AzureDBConnection"*/)
        {
        }

        public UserDBContext(string connectionKey)
            : base(connectionKey)
        {
            OnModelCreating(new DbModelBuilder());
        }

        public IDbSet<User> Users { get; set; }
    }
    #endregion
}
