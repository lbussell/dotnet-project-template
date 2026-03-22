// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Classlib.Tests;

[TestClass]
public class Class1Tests
{
    [TestMethod]
    public void Class1_CanBeInstantiated()
    {
        Class1 instance = new();

        Assert.IsNotNull(instance);
    }
}
