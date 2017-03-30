﻿namespace Microsoft.ApplicationInsights.Web
{
    using System.Collections.Generic;
    using System.Web;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OperationCorrelationTelemetryInitializerTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            ActivityHelpers.StopRequestActivity();
        }

        [TestMethod]
        public void InitializeDoesNotThrowWhenHttpContextIsNull()
        {
            var source = new OperationCorrelationTelemetryInitializer();
            source.Initialize(new RequestTelemetry());
        }

        [TestMethod]
        public void DefaultHeadersOperationCorrelationTelemetryInitializerAreSet()
        {
            var initializer = new OperationCorrelationTelemetryInitializer();
            Assert.AreEqual(RequestResponseHeaders.StandardParentIdHeader, initializer.ParentOperationIdHeaderName);
            Assert.AreEqual(RequestResponseHeaders.StandardRootIdHeader, initializer.RootOperationIdHeaderName);
        }

        [TestMethod]
        public void CustomHeadersOperationCorrelationTelemetryInitializerAreSetProperly()
        {
            var initializer = new OperationCorrelationTelemetryInitializer();
            initializer.ParentOperationIdHeaderName = "myParentHeader";
            initializer.RootOperationIdHeaderName = "myRootHeader";

            Assert.AreEqual("myParentHeader", ActivityHelpers.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", ActivityHelpers.RootOperationIdHeaderName);

            Assert.AreEqual("myParentHeader", initializer.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", initializer.RootOperationIdHeaderName);
        }

        [TestMethod]
        public void OperationContextIsSetForNonRequestTelemetry()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "|guid.1",
                ["Correlation-Context"] = "k1=v1,k2=v2,k1=v3"
            });

            // simulate OnBegin behavior:
            // create telemetry and start activity for children
            var requestTelemetry = source.FakeContext.CreateRequestTelemetryPrivate();
            ActivityHelpers.StartActivity(source.FakeContext);
            
            // lost Acitivity / call context
            ActivityHelpers.StopRequestActivity();

            var exceptionTelemetry = new ExceptionTelemetry();
            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Context.Operation.Id, exceptionTelemetry.Context.Operation.Id);
            Assert.AreEqual(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);

            Assert.AreEqual(2, exceptionTelemetry.Context.Properties.Count);
            Assert.AreEqual("v1", exceptionTelemetry.Context.Properties["k1"]);
            Assert.AreEqual("v2", exceptionTelemetry.Context.Properties["k2"]);
        }

        [TestMethod]
        public void OperationContextIsNotUpdatedIfOperationIdIsSet()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "|guid.1",
                ["Correaltion-Context"] = "k1=v1"
            });

            // create telemetry and immediately clean call context/activity
            source.FakeContext.CreateRequestTelemetryPrivate();
            ActivityHelpers.StopRequestActivity();

            var exceptionTelemetry = new ExceptionTelemetry();
            exceptionTelemetry.Context.Operation.Id = "guid";
            source.Initialize(exceptionTelemetry);

            Assert.IsNull(exceptionTelemetry.Context.Operation.ParentId);

            Assert.AreEqual(0, exceptionTelemetry.Context.Properties.Count);
        }

        private class TestableOperationCorrelationTelemetryInitializer : OperationCorrelationTelemetryInitializer
        {
            private readonly HttpContext fakeContext;

            public TestableOperationCorrelationTelemetryInitializer(IDictionary<string, string> headers = null)
            {
                this.fakeContext = HttpModuleHelper.GetFakeHttpContext(headers);
            }

            public HttpContext FakeContext
            {
                get { return this.fakeContext; }
            }

            protected override HttpContext ResolvePlatformContext()
            {
                return this.fakeContext;
            }
        }
    }
}