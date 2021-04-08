﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;

namespace Litium.SampleApps.Erp.WebHooks
{
    /// <summary>
    /// Provides an <see cref="IWebHookReceiver"/> implementation which supports WebHooks generated by Litium.
    /// A sample WebHook URI is '<c>https://&lt;host&gt;/api/webhooks/incoming/litium</c>'.
    /// </summary>
    public class LitiumReceiver : WebHookReceiver
    {
        internal const string RecName = "Litium";

        internal const int SecretMinLength = 32;
        internal const int SecretMaxLength = 128;

        internal const string EchoParameter = "echo";
        internal const string SignatureHeaderKey = "sha256";
        internal const string SignatureHeaderValueTemplate = SignatureHeaderKey + "={0}";
        internal const string SignatureHeaderName = "ms-signature";

        internal const string NotificationsKey = "Notifications";
        internal const string ActionKey = "Action";

        /// <summary>
        /// Gets the receiver name for this receiver.
        /// </summary>
        public static string ReceiverName => RecName;

        /// <inheritdoc />
        public override string Name => RecName;

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> ReceiveAsync(string id, HttpRequestContext context, HttpRequestMessage request)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Method == HttpMethod.Post)
            {
                //await VerifySignature(id, request);

                // Read the request entity body
                var data = await ReadAsJsonAsync(request);

                // Get the event actions
                var actions = GetActions(data, request);

                // Call registered handlers
                return await ExecuteWebHookAsync(id, context, request, actions, data);
            }
            else if (request.Method == HttpMethod.Get)
            {
                return await WebHookVerification(id, request);
            }
            else
            {
                return CreateBadMethodResponse(request);
            }
        }

        /// <summary>
        /// Verifies that the signature header matches that of the actual body.
        /// </summary>
        protected virtual async Task VerifySignature(string id, HttpRequestMessage request)
        {
            var secretKey = await GetReceiverConfig(request, Name, id, SecretMinLength, SecretMaxLength);

            // Get the expected hash from the signature header
            var header = GetRequestHeader(request, SignatureHeaderName);
            var values = header.SplitAndTrim('=');
            if (values.Length != 2 || !string.Equals(values[0], SignatureHeaderKey, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"Invalid '{SignatureHeaderName}' header value. Expecting a value of '{SignatureHeaderKey}= <value>'.";
                request.GetConfiguration().DependencyResolver.GetLogger().Error(message);
                var invalidHeader = request.CreateErrorResponse(HttpStatusCode.BadRequest, message);
                throw new HttpResponseException(invalidHeader);
            }

            byte[] expectedHash;
            try
            {
                expectedHash = EncodingUtilities.FromHex(values[1]);
            }
            catch (Exception ex)
            {
                var message = $"The '{SignatureHeaderName}' header value is invalid. It must be a valid hex-encoded string.";
                request.GetConfiguration().DependencyResolver.GetLogger().Error(message, ex);
                var invalidEncoding = request.CreateErrorResponse(HttpStatusCode.BadRequest, message);
                throw new HttpResponseException(invalidEncoding);
            }

            // Compute the actual hash of the request body
            byte[] actualHash;
            var secret = Encoding.UTF8.GetBytes(secretKey);
            using (var hasher = new HMACSHA256(secret))
            {
                var data = await request.Content.ReadAsByteArrayAsync();
                actualHash = hasher.ComputeHash(data);
            }

            // Now verify that the actual hash matches the expected hash.
            if (!SecretEqual(expectedHash, actualHash))
            {
                var badSignature = CreateBadSignatureResponse(request, SignatureHeaderName);
                throw new HttpResponseException(badSignature);
            }
        }

        /// <summary>
        /// Creates a response to a WebHook verification GET request.
        /// </summary>
        protected virtual async Task<HttpResponseMessage> WebHookVerification(string id, HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Verify that we have the secret as an app setting
            await GetReceiverConfig(request, Name, id, SecretMinLength, SecretMaxLength);

            // Get the 'echo' parameter and echo it back to caller
            var queryParameters = request.RequestUri.ParseQueryString();
            var echo = queryParameters[EchoParameter];
            if (string.IsNullOrEmpty(echo))
            {
                var message = $"The WebHook verification request must contain a '{EchoParameter}' query parameter which will get echoed back in a successful response.";
                request.GetConfiguration().DependencyResolver.GetLogger().Error(message);
                var noEcho = request.CreateErrorResponse(HttpStatusCode.BadRequest, message);
                return noEcho;
            }

            // Return the echo response
            var echoResponse = request.CreateResponse();
            echoResponse.Content = new StringContent(echo);
            return echoResponse;
        }

        /// <summary>
        /// Gets the notification actions form the given <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The request body.</param>
        /// <param name="request">The current <see cref="HttpRequestMessage"/>.</param>
        /// <returns>A collection of actions.</returns>
        protected virtual IEnumerable<string> GetActions(JObject data, HttpRequestMessage request)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                var actions = new List<string>();
                var notifications = data.Value<JArray>(NotificationsKey);
                if (notifications != null)
                {
                    foreach (JObject e in notifications)
                    {
                        var action = e.Value<string>(ActionKey);
                        if (action != null)
                        {
                            actions.Add(action);
                        }
                    }
                }
                return actions;
            }
            catch (Exception ex)
            {
                var message = $"Could not parse WebHook data: {ex.Message}";
                request.GetConfiguration().DependencyResolver.GetLogger().Error(message, ex);
                var invalidData = request.CreateErrorResponse(HttpStatusCode.BadRequest, message);
                throw new HttpResponseException(invalidData);
            }
        }
    }
}
