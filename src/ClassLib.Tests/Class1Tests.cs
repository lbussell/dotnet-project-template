// SPDX-FileCopyrightText: Logan Bussell <https://github.com/lbussell>
// SPDX-License-Identifier: MIT

namespace MyCompany.MySolution.ClassLib.Tests;

using MyCompany.MySolution.ClassLib;

public class Class1Tests
{
    [Fact]
    public void Test1()
    {
        Class1.Greeting.Should().ContainEquivalentOf("hello");
    }
}
