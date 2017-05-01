﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
#if !NET40
    using System.Web;
#endif
    using Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation.Operation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
#if NET40
    using Microsoft.ApplicationInsights.Net40;
#endif
    using Microsoft.ApplicationInsights.Web.Implementation;

    /// <summary>
    /// Concrete class with all processing logic to generate RDD data from the callbacks
    /// received from Profiler instrumentation for HTTP .   
    /// </summary>
    internal sealed class ProfilerHttpProcessing : HttpProcessing
    {
        internal ObjectInstanceBasedOperationHolder TelemetryTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilerHttpProcessing"/> class.
        /// </summary>
        public ProfilerHttpProcessing(TelemetryConfiguration configuration, string agentVersion, ObjectInstanceBasedOperationHolder telemetryTupleHolder, bool setCorrelationHeaders, ICollection<string> correlationDomainExclusionList, string appIdEndpoint)
            : base(configuration, SdkVersionUtils.GetSdkVersion("rdd" + RddSource.Profiler + ":"), agentVersion, setCorrelationHeaders, correlationDomainExclusionList, appIdEndpoint)
        {
            if (telemetryTupleHolder == null)
            {
                throw new ArgumentNullException("telemetryTupleHolder");
            }

            this.TelemetryTable = telemetryTupleHolder;
        }

        #region Http callbacks

        /// <summary>
        /// On begin callback for GetResponse.
        /// </summary>
        /// <param name="thisObj">This object.</param>
        /// <returns>The context for end callback.</returns>
        public object OnBeginForGetResponse(object thisObj)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On end callback for GetResponse.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="returnValue">The return value.</param>
        /// <param name="thisObj">This object.</param>
        /// <returns>The resulting return value.</returns>
        public object OnEndForGetResponse(object context, object returnValue, object thisObj)
        {
            this.OnEnd(null, thisObj, returnValue);
            return returnValue;
        }

        /// <summary>
        /// On exception callback for GetResponse callback.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="exception">The exception object.</param>
        /// <param name="thisObj">This object.</param>        
        public void OnExceptionForGetResponse(object context, object exception, object thisObj)
        {
            this.OnEnd(exception, thisObj, null);
        }
        
        /// <summary>
        /// On begin callback for GetRequestStream callback.
        /// </summary>
        /// <param name="thisObj">This object.</param>
        /// <param name="transportContext">The transport context parameter.</param>
        /// <returns>The context for end callback.</returns>
        public object OnBeginForGetRequestStream(object thisObj, object transportContext)
        {
            return this.OnBegin(thisObj);
        }       

        /// <summary>
        /// On exception for GetRequestStream callback.
        /// Note: There is no call back required for GetRequestStream except on exception cases.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="thisObj">This object.</param>
        /// <param name="transportContext">The transport context parameter.</param>
        public void OnExceptionForGetRequestStream(object context, object exception, object thisObj, object transportContext)
        {
            this.OnEnd(exception, thisObj, null);
        }

        /// <summary>
        /// On begin for BeginGetResponse callback.
        /// </summary>
        /// <param name="thisObj">This object.</param>
        /// <param name="callback">The callback parameter.</param>
        /// <param name="state">The state parameter.</param>
        /// <returns>The context for end callback.</returns>
        public object OnBeginForBeginGetResponse(object thisObj, object callback, object state)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On end for EndGetResponse callbacks.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="returnValue">The return value.</param>
        /// <param name="thisObj">This object.</param>
        /// <param name="asyncResult">The asyncResult parameter.</param>
        /// <returns>The return value passed.</returns>
        public object OnEndForEndGetResponse(object context, object returnValue, object thisObj, object asyncResult)
        {
            this.OnEnd(null, thisObj, returnValue);
            return returnValue;
        }

        /// <summary>
        /// On exception for EndGetResponse callbacks.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="thisObj">This object.</param>
        /// <param name="asyncResult">The asyncResult parameter.</param>
        public void OnExceptionForEndGetResponse(object context, object exception, object thisObj, object asyncResult)
        {
            this.OnEnd(exception, thisObj, null);
        }

        /// <summary>
        /// On begin for BeginGetRequestStream callback.
        /// </summary>
        /// <param name="thisObj">This object.</param>
        /// <param name="callback">The callback parameter.</param>
        /// <param name="state">The state parameter.</param>
        /// <returns>The context for end callback.</returns>
        public object OnBeginForBeginGetRequestStream(object thisObj, object callback, object state)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On exception for EndGetRequestStream callback.
        /// Note: There is no call back required for EndGetRequestStream except on exception cases.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="thisObj">This object.</param>
        /// <param name="asyncResult">The asyncResult parameter.</param>
        /// <param name="transportContext">The transportContext parameter.</param>
        public void OnExceptionForEndGetRequestStream(object context, object exception, object thisObj, object asyncResult, object transportContext)
        {
            this.OnEnd(exception, thisObj, null);
        }

        #endregion // Http callbacks

        /// <summary>
        /// Implemented by the derived class for adding the tuple to its specific cache.
        /// </summary>
        /// <param name="webRequest">The request which acts the key.</param>
        /// <param name="telemetry">The dependency telemetry for the tuple.</param>
        /// <param name="isCustomCreated">Boolean value that tells if the current telemetry item is being added by the customer or not.</param>
        protected override void AddTupleForWebDependencies(WebRequest webRequest, DependencyTelemetry telemetry, bool isCustomCreated)
        {
            var telemetryTuple = new Tuple<DependencyTelemetry, bool>(telemetry, isCustomCreated);
            this.TelemetryTable.Store(webRequest, telemetryTuple);
        }

        /// <summary>
        /// Implemented by the derived class for getting the tuple from its specific cache.
        /// </summary>
        /// <param name="webRequest">The request which acts as the key.</param>
        /// <returns>The tuple for the given request.</returns>
        protected override Tuple<DependencyTelemetry, bool> GetTupleForWebDependencies(WebRequest webRequest)
        {
            return this.TelemetryTable.Get(webRequest);
        }

        /// <summary>
        /// Implemented by the derived class for removing the tuple from its specific cache.
        /// </summary>
        /// <param name="webRequest">The request which acts as the key.</param>
        protected override void RemoveTupleForWebDependencies(WebRequest webRequest)
        {
            this.TelemetryTable.Remove(webRequest);
        }
    }
}