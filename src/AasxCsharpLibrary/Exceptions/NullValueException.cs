﻿/*
Copyright (c) 2018-2023 Festo AG & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/
using System;

namespace AdminShellNS.Exceptions
{
    public class NullValueException : Exception
    {
        public NullValueException(string field) : base($"The field {field} is null!!")
        {

        }
    }
}
