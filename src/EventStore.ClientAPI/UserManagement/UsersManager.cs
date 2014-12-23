﻿using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.UserManagement
{
    /// <summary>
    /// API for managing users in the Event Store through C# code. Communicates
    /// with the Event Store over the RESTful API.
    /// </summary>
    public class UsersManager
    {
        private readonly UsersClient _client;

        private readonly IPEndPoint _httpEndPoint;

        /// <summary>
        /// Creates a new instance of <see cref="UsersManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        /// <param name="operationTimeout"></param>
        public UsersManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan operationTimeout)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            this._client = new UsersClient(log, operationTimeout);
            this._httpEndPoint = httpEndPoint;
        }

        /// <summary>
/// Asynchronously enables a user
/// </summary>
/// <param name="login">The login of the user to enable</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task EnableAsync(string login, UserCredentials userCredentials =null)
        {
Ensure.NotNullOrEmpty(login, "login");
return this._client.Enable(this._httpEndPoint, login, userCredentials);
        }
        
        /// <summary>
        /// Asynchronously disables a user
        /// </summary>
        /// <param name="login">The login of the user to disable</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisableAsync(string login, UserCredentials userCredentials =null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            return this._client.Disable(this._httpEndPoint, login, userCredentials);
        }
        /// <summary>
        /// Asynchronously deletes a user
        /// </summary>
        /// <param name="login">The login of the user.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string login, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            return this._client.Delete(this._httpEndPoint, login, userCredentials);
        }
        
        /// <summary>
        /// Asynchronously lists all users.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing user full names and logins.</returns>
        public Task<string> ListAllAsync(UserCredentials userCredentials = null)
        { 
            return this._client.ListAll(this._httpEndPoint, userCredentials);
        }
        
        /// <summary>
        /// Asynchronously gets the current users details
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing the current users full name and login.</returns>
        public Task<string> GetCurrentUserAsync(UserCredentials userCredentials)
        {
            return this._client.GetCurrentUser(this._httpEndPoint, userCredentials);
        }
        
        /// <summary>
        /// Asynchronously gets a users details
        /// </summary>
        /// <param name="login">the login for the user who's details should be retrieved.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing the specified users full name and login.</returns>
        public Task<string> GetUserAsync(string login, UserCredentials userCredentials)
        {
            Ensure.NotNullOrEmpty(login, "login");
            return this._client.GetUser(this._httpEndPoint, login, userCredentials);
        }
    
        /// <summary>
    /// Create a new user.
    /// </summary>
    /// <param name="login">The login name of the new user.</param>
    /// <param name="fullName">The full name of the new user.</param>
    /// <param name="groups">The groups the new user should be a member of.</param>
    /// <param name="password">The new users password.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>A task representing the operation.</returns>
        public Task CreateUser(string login, string fullName, string[] groups, string password, UserCredentials userCredentials = null)
{
Ensure.NotNullOrEmpty(login, "login");
            Ensure.NotNullOrEmpty(fullName, "fullName");
Ensure.NotNull(groups, "groups");
            Ensure.NotNullOrEmpty(password, "password");
            return this._client.CreateUser(this._httpEndPoint, new UserCreationInformation(login, fullName, groups, password), userCredentials);
}
        
        /// <summary>
        /// Update an existing user.
        /// </summary>
        /// <param name="login">The login name of the user to update.</param>
        /// <param name="fullName">The full name of the user being updated.</param>
        /// <param name="groups">The groups the updated user should be a member of.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>A task representing the operation.</returns>
        public Task UpdateUser(string login, string fullName, string[] groups, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            Ensure.NotNullOrEmpty(fullName, "fullName");
            Ensure.NotNull(groups, "groups");
            return this._client.UpdateUser(this._httpEndPoint, login, new UserUpdateInformation(fullName, groups), userCredentials);
        }
        
        /// <summary>
        /// Change a users password.
        /// </summary>
        /// <param name="login">The login of the user who's password should be changed</param>
        /// <param name="oldPassword">The users old password.</param>
        /// <param name="newPassword">The users new password</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>A task representing the operation.</returns>
        public Task ChangePassword(string login, string oldPassword, string newPassword, UserCredentials userCredentials = null)
            {
                Ensure.NotNullOrEmpty(login, "login");
                Ensure.NotNullOrEmpty(oldPassword, "oldPassword");
                Ensure.NotNullOrEmpty(newPassword, "newPassword");
                return this._client.ChangePassword(this._httpEndPoint, login, new ChangePasswordDetails(oldPassword, newPassword), userCredentials);
            }
        
        /// <summary>
        /// Reset a users password.
        /// </summary>
        /// <param name="login">The login of the user who's password should be reset.</param>
        /// <param name="newPassword">The users new password</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>A task representing the operation.</returns>
        public Task ResetPassword(string login, string newPassword, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            Ensure.NotNullOrEmpty(newPassword, "newPassword");
            return this._client.ResetPassword(this._httpEndPoint, login, new ResetPasswordDetails(newPassword), userCredentials);
        }
    }
}
