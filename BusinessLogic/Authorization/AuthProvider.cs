using AutoMapper;
using Microsoft.AspNetCore.Identity;
using DataAccess.Entity;
using BusinessLogic.Authorization.Entity;
using BusinessLogic.Authorization.Exceptions;
using BusinessLogic.Users.Model;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Models;

namespace BusinessLogic.Authorization;

public class AuthProvider (
    SignInManager<User> _signInManager,
    UserManager<User> _userManager,
    IHttpClientFactory httpClientFactory,
    string identityServerUri,
    string clientId,
    string clientSecret,
    IMapper mapper) : IAuthProvider
{
    public async Task<TokensResponse> AuthorizeUser(string email, string password, string userName)
    {
        var userByEmail = await _userManager.FindByEmailAsync(email);
        if (userByEmail is null)
        {
            throw new AuthExceptions(Excep.UserNotFound);
        }
        
        var checkPassword = await _signInManager.CheckPasswordSignInAsync(userByEmail, password, false);
        if (!checkPassword.Succeeded)
        {
            throw new AuthExceptions(Excep.IncorrectPassOrEm);
        }
        
        var client = httpClientFactory.CreateClient();
        var endPoint = await client.GetDiscoveryDocumentAsync(identityServerUri);
        if (endPoint.IsError)
        {
            throw new AuthExceptions(Excep.IdentityServerError);
        }

        var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = endPoint.TokenEndpoint,
            GrantType = GrantType.ResourceOwnerPassword,
            ClientId = clientId,
            ClientSecret = clientSecret,
            UserName = userName,
            Password = password,
            Scope = "api offline_access"
        });
        if (tokenResponse.IsError)
        {
            throw new Exception();
        }
        return new TokensResponse
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
        };
    }

    public async Task<UserModel> RegisterUser(string email, string password, string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new AuthExceptions("Имя пользователя не может быть пустым или содержать только пробелы.");
        }

        var trimmedUserName = userName.Trim();

        if (!trimmedUserName.All(char.IsLetterOrDigit))
        {
            throw new AuthExceptions("Имя пользователя может содержать только буквы и цифры.");
        }
        
        var userByEmail = await _userManager.FindByEmailAsync(email);
        if (userByEmail is not null)
        {
            throw new AuthExceptions(Excep.UserAlreadyExists);
        }
        

        var user = new User
        {
            UserName = userName,
            Email = email,
            CreationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            ModificationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            ExternalId = Guid.NewGuid()
        };
        
        try
        {
            var userCreate = await _userManager.CreateAsync(user, password);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            throw new Exception($"Ошибка сохранения: {inner}", ex);
        }

        var newUser = await _userManager.FindByEmailAsync(email);
        return mapper.Map<UserModel>(newUser);
    }
}