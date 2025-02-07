﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core.Testing;
using NUnit.Framework;

namespace Azure.Core.Tests
{
    public class ResponseTests
    {
        [Test]
        public void ValueIsAccessible()
        {
            var response = Response.FromValue(new TestPayload("test_name"), response: null);
            TestPayload value = response.Value;

            Assert.IsNotNull(value);
            Assert.AreEqual("test_name", value.Name);
        }

        [Test]
        public void ValueObtainedFromCast()
        {
            var response = Response.FromValue(new TestPayload("test_name"), response: null);
            TestPayload value = response;

            Assert.IsNotNull(value);
            Assert.AreEqual("test_name", value.Name);
        }

        [Test]
        public void ValueThrowsIfUnspecified()
        {
            var response = new NoBodyResponse<TestPayload>(new MockResponse(304));
            bool throws = false;
            try
            {
                TestPayload value = response.Value;
            }
            catch
            {
                throws = true;
            }

            Assert.True(throws);
        }

        [Test]
        public void ValueThrowsFromCastIfUnspecified()
        {
            var response = new NoBodyResponse<TestPayload>(new MockResponse(304));

            bool throws = false;
            try
            {
                TestPayload value = response;
            }
            catch
            {
                throws = true;
            }

            Assert.True(throws);
        }

        internal class TestPayload
        {
            public string Name { get; }

            public TestPayload(string name)
            {
                Name = name;
            }
        }
    }

}
