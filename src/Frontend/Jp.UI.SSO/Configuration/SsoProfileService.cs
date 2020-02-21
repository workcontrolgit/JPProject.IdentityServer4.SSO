﻿using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Jp.UI.SSO.Util;
using JPProject.Domain.Core.StringUtils;
using JPProject.Sso.AspNetIdentity.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Jp.UI.SSO.Configuration
{
    public class SsoProfileService : IProfileService
    {
        protected UserManager<UserIdentity> UserManager;
        private readonly ILogger<DefaultProfileService> Logger;
        private readonly IUserClaimsPrincipalFactory<UserIdentity> _claimsFactory;

        public SsoProfileService(
            UserManager<UserIdentity> userManager,
            ILogger<DefaultProfileService> logger,
            IUserClaimsPrincipalFactory<UserIdentity> claimsFactory)
        {
            UserManager = userManager;
            Logger = logger;
            _claimsFactory = claimsFactory;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var user = await UserManager.GetUserAsync(context.Subject);
            // Adding the current claims from user
            var principal = await _claimsFactory.CreateAsync(user);
            var claimsUser = principal.Claims.ToList();
            var subjectClaims = context.Subject.Claims.ToList();

            subjectClaims.Merge(claimsUser);
            subjectClaims.AddIfDontExist(new Claim("username", user.UserName));

            // Email is loaded default by IdentityServer4
            //claims.Add(new Claim(JwtClaimTypes.Email, user.Email, ClaimValueTypes.Email));

            if (user.Birthdate.HasValue)
                subjectClaims.AddIfDontExist(new Claim(JwtClaimTypes.BirthDate, user.Birthdate.Value.ToString(CultureInfo.CurrentCulture), ClaimValueTypes.Date));

            if (user.Name.IsPresent())
                subjectClaims.AddIfDontExist(new Claim(JwtClaimTypes.Name, user.Name));
            else
                subjectClaims.AddIfDontExist(new Claim(JwtClaimTypes.Name, user.UserName));


            if (user.Picture.IsPresent())
                subjectClaims.AddIfDontExist(new Claim(JwtClaimTypes.Picture, user.Picture));

            if (user.SocialNumber.IsPresent())
                subjectClaims.AddIfDontExist(new Claim("social_number", user.SocialNumber));


            if (subjectClaims.All(a => a.Type != JwtClaimTypes.Role))
            {
                var roles = await UserManager.GetRolesAsync(user);
                subjectClaims.AddRange(roles.Select(s => new Claim(JwtClaimTypes.Role, s)));
            }


            context.LogProfileRequest(Logger);
            context.AddRequestedClaims(subjectClaims);
            context.LogIssuedClaims(Logger);
        }


        public async Task IsActiveAsync(IsActiveContext context)
        {
            var user = await UserManager.GetUserAsync(context.Subject);

            // In case admin is accidentally blocked.
            var active = user.LockoutEnabled && user.LockoutEnd.GetValueOrDefault(DateTimeOffset.UtcNow.Date) > DateTimeOffset.UtcNow;
            if (!active)
                active = await UserManager.IsInRoleAsync(user, "Administrator");

            context.IsActive = active;
        }
    }
}