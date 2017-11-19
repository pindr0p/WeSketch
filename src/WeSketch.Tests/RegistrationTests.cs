﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WeSketch.Tests
{

    /// <summary>
    /// Testing registration - CreateUser
    /// </summary>
    [TestFixture]
    class RegistrationTests
    {

        [Test]
        [TestCase("", "", "")]
        [TestCase("", "somepassword", "")]
        [TestCase("someusername", "", "")]
        [TestCase("someusername", "", "someemail")]
        [TestCase("", "", "someemail")]
        [TestCase("someusername", "somepassword", "someemail")]
        public void IsInvalidCreate_User(string userName, string email, string password)
        {
            Registration reg = new Registration();
            var ex = Assert.Catch<Exception>(() => reg.CreateUser(userName, email, password));
            StringAssert.Contains("Error", ex.Message);
        }
    }
}
