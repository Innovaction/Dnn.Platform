﻿// 
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// 
#region Usings

using System;
using System.Collections;
using System.IO;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;

using DotNetNuke.Common;
using DotNetNuke.Abstractions;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using DotNetNuke.Security;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Skins.Controls;

#endregion

namespace DotNetNuke.Modules.Admin.Modules
{
    public partial class Import : PortalModuleBase
    {
        private readonly INavigationManager _navigationManager;
        public Import()
        {
            _navigationManager = DependencyProvider.GetRequiredService<INavigationManager>();
        }

        #region Private Members

        private new int ModuleId = -1;
        private ModuleInfo _module;

        private ModuleInfo Module
        {
            get
            {
                return _module ?? (_module = ModuleController.Instance.GetModule(ModuleId, TabId, false));
            }
        }

        private string ReturnURL
        {
            get
            {
                return UrlUtils.ValidReturnUrl(Request.Params["ReturnURL"]) ?? _navigationManager.NavigateURL();
            }
        }

        #endregion

        #region Private Methods

        private string ImportModule()
        {
            var strMessage = "";
            if (Module != null)
            {
                if (!String.IsNullOrEmpty(Module.DesktopModule.BusinessControllerClass) && Module.DesktopModule.IsPortable)
                {
                    try
                    {
                        var objObject = Reflection.CreateObject(Module.DesktopModule.BusinessControllerClass, Module.DesktopModule.BusinessControllerClass);
                        if (objObject is IPortable)
                        {
                            var xmlDoc = new XmlDocument { XmlResolver = null };
                            try
                            {
                                var content = XmlUtils.RemoveInvalidXmlCharacters(txtContent.Text);
                                xmlDoc.LoadXml(content);
                            }
                            catch
                            {
                                strMessage = Localization.GetString("NotValidXml", LocalResourceFile);
                            }
                            if (String.IsNullOrEmpty(strMessage))
                            {
                                var strType = xmlDoc.DocumentElement.GetAttribute("type");
                                if (strType == Globals.CleanName(Module.DesktopModule.ModuleName) || strType == Globals.CleanName(Module.DesktopModule.FriendlyName))
                                {
                                    var strVersion = xmlDoc.DocumentElement.GetAttribute("version");
                                    // DNN26810 if rootnode = "content", import only content(the old way)
                                    if (xmlDoc.DocumentElement.Name.ToLowerInvariant() == "content" )
                                    {
                                        ((IPortable)objObject).ImportModule(ModuleId, xmlDoc.DocumentElement.InnerXml, strVersion, UserInfo.UserID);
                                    }
                                    // otherwise (="module") import the new way
                                    else
                                    {
                                        ModuleController.DeserializeModule(xmlDoc.DocumentElement, Module, PortalId, TabId);
                                    }
                                    Response.Redirect(_navigationManager.NavigateURL(), true);
                                }
                                else
                                {
                                    strMessage = Localization.GetString("NotCorrectType", LocalResourceFile);
                                }
                            }
                        }
                        else
                        {
                            strMessage = Localization.GetString("ImportNotSupported", LocalResourceFile);
                        }
                    }
                    catch
                    {
                        strMessage = Localization.GetString("Error", LocalResourceFile);
                    }
                }
                else
                {
                    strMessage = Localization.GetString("ImportNotSupported", LocalResourceFile);
                }
            }
            return strMessage;
        }

        #endregion

        #region Event Handlers

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            if (Request.QueryString["moduleid"] != null)
            {
                Int32.TryParse(Request.QueryString["moduleid"], out ModuleId);
            }

            //Verify that the current user has access to edit this module
            if (!ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, "IMPORT", Module))
            {
                Response.Redirect(Globals.AccessDeniedURL(), true);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            cboFolders.SelectionChanged += OnFoldersIndexChanged;
            cboFiles.SelectedIndexChanged += OnFilesIndexChanged;
            cmdImport.Click += OnImportClick;

            try
            {
                if (!Page.IsPostBack)
                {
                    cmdCancel.NavigateUrl = ReturnURL;
                    cboFolders.UndefinedItem = new ListItem("<" + Localization.GetString("None_Specified") + ">", string.Empty);
                    cboFolders.Services.Parameters.Add("permission", "ADD");
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        protected void OnFoldersIndexChanged(object sender, EventArgs e)
        {
            cboFiles.Items.Clear();
            cboFiles.InsertItem(0, "<" + Localization.GetString("None_Specified") + ">", "-");
            if (cboFolders.SelectedItem == null)
            {
                return;
            }
            if (Module == null)
            {
                return;
            }

            var folder = FolderManager.Instance.GetFolder(cboFolders.SelectedItemValueAsInt);
            if (folder == null) return;

            var files = Globals.GetFileList(PortalId, "xml", false, folder.FolderPath);
            foreach (FileItem file in files)
            {
				if (file.Text.IndexOf("content." + Globals.CleanName(Module.DesktopModule.ModuleName) + ".", System.StringComparison.Ordinal) != -1)
                {
					cboFiles.AddItem(file.Text.Replace("content." + Globals.CleanName(Module.DesktopModule.ModuleName) + ".", ""), file.Value);
                }

                //legacy support for files which used the FriendlyName
                if (Globals.CleanName(Module.DesktopModule.ModuleName) == Globals.CleanName(Module.DesktopModule.FriendlyName))
                {
                    continue;
                }

				if (file.Text.IndexOf("content." + Globals.CleanName(Module.DesktopModule.FriendlyName) + ".", System.StringComparison.Ordinal) != -1)
                {
					cboFiles.AddItem(file.Text.Replace("content." + Globals.CleanName(Module.DesktopModule.FriendlyName) + ".", ""), file.Value);
                }
            }
        }

        protected void OnFilesIndexChanged(object sender, EventArgs e)
        {
            if (cboFolders.SelectedItem == null) return;
            var folder = FolderManager.Instance.GetFolder(cboFolders.SelectedItemValueAsInt);
            if (folder == null) return;

	        if (string.IsNullOrEmpty(cboFiles.SelectedValue) || cboFiles.SelectedValue == "-")
	        {
				txtContent.Text = string.Empty;
		        return;
	        }
	        try
	        {
				var fileId = Convert.ToInt32(cboFiles.SelectedValue);
		        var file = DotNetNuke.Services.FileSystem.FileManager.Instance.GetFile(fileId);
				using (var streamReader = new StreamReader(DotNetNuke.Services.FileSystem.FileManager.Instance.GetFileContent(file)))
				{
					txtContent.Text = streamReader.ReadToEnd();
				}
	        }
	        catch (Exception)
	        {
		        txtContent.Text = string.Empty;
	        }
        }

        protected void OnImportClick(object sender, EventArgs e)
        {
            try
            {
                if (Module != null)
                {
                    var strMessage = ImportModule();
                    if (String.IsNullOrEmpty(strMessage))
                    {
                        Response.Redirect(ReturnURL, true);
                    }
                    else
                    {
                        UI.Skins.Skin.AddModuleMessage(this, strMessage, ModuleMessage.ModuleMessageType.RedError);
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        #endregion

    }
}
