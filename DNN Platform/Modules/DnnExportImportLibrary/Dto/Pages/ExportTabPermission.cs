﻿// 
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// 
using System;

// ReSharper disable InconsistentNaming

namespace Dnn.ExportImport.Dto.Pages
{
    public class ExportTabPermission : BasicExportImportDto
    {
        public int TabPermissionID { get; set; }
        public int TabID { get; set; }
        public int PermissionID { get; set; }
        public string PermissionCode { get; set; }
        public string PermissionKey { get; set; }
        public string PermissionName { get; set; }
        public bool AllowAccess { get; set; }
        public int? RoleID { get; set; }
        public string RoleName { get; set; }
        public int? UserID { get; set; }
        public string Username { get; set; }
        public int? ModuleDefID { get; set; }
        public string FriendlyName { get; set; } // this is ModuleDefinition.FriendlyName

        public int? CreatedByUserID { get; set; }
        public DateTime? CreatedOnDate { get; set; }
        public int? LastModifiedByUserID { get; set; }
        public DateTime? LastModifiedOnDate { get; set; }

        public string CreatedByUserName { get; set; }
        public string LastModifiedByUserName { get; set; }
    }
}
