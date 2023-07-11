﻿/*
Copyright (c) 2018-2023 

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;
using AdminShellNS;

namespace IO.Swagger.Model
{
    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public partial class AssetAdministrationShellAndAsset
    {
        /// <summary>
        /// Gets or sets AssetAdministrationShell
        /// </summary>
        [DataMember(Name = "aas")]
        public AasCore.Aas3_0_RC02.AssetAdministrationShell aas;

        /// <summary>
        /// Gets or sets Asset
        /// </summary>
        [DataMember(Name = "asset")]
        public AasCore.Aas3_0_RC02.AssetInformation asset;
    }
}
