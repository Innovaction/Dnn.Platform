﻿// 
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// 
#region Usings

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Users;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Social.Notifications;
using DotNetNuke.Services.Social.Messaging.Internal;
using DotNetNuke.Abstractions;

#endregion

namespace DotNetNuke.UI.Skins.Controls
{
    public partial class UserAndLogin : SkinObjectBase
    {
        private const string MyFileName = "UserAndLogin.ascx";
        private readonly INavigationManager _navigationManager;

        protected string AvatarImageUrl => UserController.Instance.GetUserProfilePictureUrl(PortalSettings.UserId, 32, 32);

        public UserAndLogin()
        {
            _navigationManager = Globals.DependencyProvider.GetRequiredService<INavigationManager>();
        }

        protected bool CanRegister
        {
            get
            {
                return ((PortalSettings.UserRegistration != (int) Globals.PortalRegistrationType.NoRegistration)
                    && (PortalSettings.Users < PortalSettings.UserQuota || PortalSettings.UserQuota == 0));
            }
        }

        protected string DisplayName
        {
            get
            {
                return PortalSettings.UserInfo.DisplayName;
            }
        }

        protected bool IsAuthenticated
        {
            get
            {
                return Request.IsAuthenticated;
            }
        }

        protected string LoginUrl
        {
            get
            {
                string returnUrl = HttpContext.Current.Request.RawUrl;
                if (returnUrl.IndexOf("?returnurl=", StringComparison.Ordinal) != -1)
                {
                    returnUrl = returnUrl.Substring(0, returnUrl.IndexOf("?returnurl=", StringComparison.Ordinal));
                }
                returnUrl = HttpUtility.UrlEncode(returnUrl);

                return Globals.LoginURL(returnUrl, (Request.QueryString["override"] != null));
            }
        }

		protected string LoginUrlForClickEvent
		{
			get
			{
				var url = LoginUrl;

				if (UsePopUp)
				{
					return "return " + UrlUtils.PopUpUrl(HttpUtility.UrlDecode(LoginUrl), this, PortalSettings, true, false, 300, 650);
				}

				return string.Empty;
			}
		}

        protected bool UsePopUp
        {
            get
            {
                return PortalSettings.EnablePopUps
                    && PortalSettings.LoginTabId == Null.NullInteger
                    && !AuthenticationController.HasSocialAuthenticationEnabled(this);
            }
        }

        protected string RegisterUrl
        {
            get
            {
                return Globals.RegisterURL(HttpUtility.UrlEncode(_navigationManager.NavigateURL()), Null.NullString);
            }
        }

		protected string RegisterUrlForClickEvent
		{
			get
			{
				if (UsePopUp)
				{
					return "return " + UrlUtils.PopUpUrl(HttpUtility.UrlDecode(RegisterUrl), this, PortalSettings, true, false, 600, 950);
				}

				return string.Empty;
			}
		}

        protected string UserProfileUrl
        {
            get
            {
                return Globals.UserProfileURL(PortalSettings.UserInfo.UserID); ;
            }
        }

        /// <summary>
        /// set this to true to show in custom 404/500 page.
        /// </summary>
        public bool ShowInErrorPage { get; set; }

        protected string LocalizeString(string key)
        {
            return Localization.GetString(key, Localization.GetResourceFile(this, MyFileName));
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            Visible = !PortalSettings.InErrorPageRequest() || ShowInErrorPage;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            registerLink.NavigateUrl = RegisterUrl;
            loginLink.NavigateUrl = LoginUrl;

            if (PortalSettings.UserId > 0)
            {
                viewProfileLink.NavigateUrl = Globals.UserProfileURL(PortalSettings.UserId);
                viewProfileImageLink.NavigateUrl = Globals.UserProfileURL(PortalSettings.UserId);
                logoffLink.NavigateUrl = _navigationManager.NavigateURL(PortalSettings.ActiveTab.TabID, "Logoff");
                editProfileLink.NavigateUrl = _navigationManager.NavigateURL(PortalSettings.UserTabId, "Profile", "userId=" + PortalSettings.UserId, "pageno=2");
                accountLink.NavigateUrl = _navigationManager.NavigateURL(PortalSettings.UserTabId, "Profile", "userId=" + PortalSettings.UserId, "pageno=1");
                messagesLink.NavigateUrl = _navigationManager.NavigateURL(GetMessageTab(), "", string.Format("userId={0}", PortalSettings.UserId));
                notificationsLink.NavigateUrl = _navigationManager.NavigateURL(GetMessageTab(), "", string.Format("userId={0}", PortalSettings.UserId), "view=notifications", "action=notifications");

                var unreadMessages = InternalMessagingController.Instance.CountUnreadMessages(PortalSettings.UserId, PortalSettings.PortalId);
                var unreadAlerts = NotificationsController.Instance.CountNotifications(PortalSettings.UserId, PortalSettings.PortalId);

                if (unreadMessages > 0)
                {
                    messageCount.Text = unreadMessages.ToString(CultureInfo.InvariantCulture);
                    messageCount.Visible = true;

                    messages.Text = unreadMessages.ToString(CultureInfo.InvariantCulture);
                    messages.ToolTip = unreadMessages == 1
                                        ? LocalizeString("OneMessage")
                                        : String.Format(LocalizeString("MessageCount"), unreadMessages);
                    messages.Visible = true;
                }

                if (unreadAlerts > 0)
                {
                    notificationCount.Text = unreadAlerts.ToString(CultureInfo.InvariantCulture);
                    notificationCount.Visible = true;
                }

                profilePicture.ImageUrl = AvatarImageUrl;
                profilePicture.AlternateText = Localization.GetString("ProfilePicture", Localization.GetResourceFile(this, MyFileName));

                if (AlwaysShowCount())
                {
                    messageCount.Visible = notificationCount.Visible = true;
                }
            }

            if (UsePopUp)
            {
                registerLink.Attributes.Add("onclick", RegisterUrlForClickEvent);
                loginLink.Attributes.Add("onclick", LoginUrlForClickEvent);
            }

        }

        private int GetMessageTab()
        {
            var cacheKey = string.Format("MessageCenterTab:{0}:{1}", PortalSettings.PortalId, PortalSettings.CultureCode);
            var messageTabId = DataCache.GetCache<int>(cacheKey);
            if (messageTabId > 0)
                return messageTabId;

            //Find the Message Tab
            messageTabId = FindMessageTab();

            //save in cache
            //NOTE - This cache is not being cleared. There is no easy way to clear this, except Tools->Clear Cache
            DataCache.SetCache(cacheKey, messageTabId, TimeSpan.FromMinutes(20));

            return messageTabId;
        }

        private int FindMessageTab()
        {
            //On brand new install the new Message Center Module is on the child page of User Profile Page
            //On Upgrade to 6.2.0, the Message Center module is on the User Profile Page
            var profileTab = TabController.Instance.GetTab(PortalSettings.UserTabId, PortalSettings.PortalId, false);
            if (profileTab != null)
            {
                var childTabs = TabController.Instance.GetTabsByPortal(profileTab.PortalID).DescendentsOf(profileTab.TabID);
                foreach (TabInfo tab in childTabs)
                {
                    foreach (KeyValuePair<int, ModuleInfo> kvp in ModuleController.Instance.GetTabModules(tab.TabID))
                    {
                        var module = kvp.Value;
                        if (module.DesktopModule.FriendlyName == "Message Center" && !module.IsDeleted)
                        {
                            return tab.TabID;
                        }
                    }
                }
            }

            //default to User Profile Page
            return PortalSettings.UserTabId;
        }

        private bool AlwaysShowCount()
        {
            const string SettingKey = "UserAndLogin_AlwaysShowCount";
            var alwaysShowCount = false;

            var portalSetting = PortalController.GetPortalSetting(SettingKey, PortalSettings.PortalId, string.Empty);
            if (!string.IsNullOrEmpty(portalSetting) && bool.TryParse(portalSetting, out alwaysShowCount))
            {
                return alwaysShowCount;
            }

            var hostSetting = HostController.Instance.GetString(SettingKey, string.Empty);
            if (!string.IsNullOrEmpty(hostSetting) && bool.TryParse(hostSetting, out alwaysShowCount))
            {
                return alwaysShowCount;
            }

            return false;
        }
    }
}
