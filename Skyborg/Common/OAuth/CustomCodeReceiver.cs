﻿using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Google.Apis.Auth.OAuth2
{
    /// <summary>
    /// OAuth 2.0 verification code receiver that runs a local server on a free port and waits for a call with the 
    /// authorization verification code.
    /// </summary>
    public class CustomCodeReceiver : ICodeReceiver
    {
        private static readonly ILogger Logger = ApplicationContext.Logger.ForType<CustomCodeReceiver>();

        /// <summary>The call back request path.</summary>
        internal const string LoopbackCallbackPath = "/authorize/";

        //HttpRequest request = HttpContext.Current.Request;
        //static string baseUrl = HttpContext.Current.Request.Url.Scheme + "://" + HttpContext.Current.Request.Url.Authority.Replace(":3979","") + ":8443" + HttpContext.Current.Request.ApplicationPath.TrimEnd('/');

        static string baseUrl = HttpContext.Current.Request.Url.Scheme + "://localhost:7777"+ HttpContext.Current.Request.ApplicationPath.TrimEnd('/');


        /// <summary>The call back format. Expects one port parameter.</summary>
        internal string LoopbackCallback = baseUrl + LoopbackCallbackPath;

        /// <summary>Close HTML tag to return the browser so it will close itself.</summary>
        internal const string ClosePageResponse =
@"<html>
  <head><title>OAuth 2.0 Authentication Token Received</title></head>
  <body>
    Received verification code. You may now close this window.
    <script type='text/javascript'>
      // This doesn't work on every browser.
      window.setTimeout(function() {
          this.focus();
          window.opener = this;
          window.open('', '_self', ''); 
          window.close(); 
        }, 1000);
      //if (window.opener) { window.opener.checkToken(); }
    </script>
  </body>
</html>";

        // Not required in NET45, but present for testing.
        /// <summary>
        /// An extremely limited HTTP server that can only do exactly what is required
        /// for this use-case.
        /// It can only serve localhost; receive a single GET request; read only the query paremters;
        /// send back a fixed response. Nothing else.
        /// </summary>
        internal class LimitedLocalhostHttpServer : IDisposable
        {
            private const int MaxRequestLineLength = 256;
            private const int MaxHeadersLength = 8192;
            private const int NetworkReadBufferSize = 1024;

            private static ILogger Logger = ApplicationContext.Logger.ForType<LimitedLocalhostHttpServer>();

            public class ServerException : Exception
            {
                public ServerException(string msg) : base(msg) { }
            }

            public static LimitedLocalhostHttpServer Start(string url)
            {
                var uri = new Uri(url);
                if (!uri.IsLoopback)
                {
                    throw new ArgumentException($"Url must be loopback, but given: '{url}'", nameof(url));
                }
                var listener = new TcpListener(IPAddress.Loopback, uri.Port);
                return new LimitedLocalhostHttpServer(listener);
            }

            private LimitedLocalhostHttpServer(TcpListener listener)
            {
                _listener = listener;
                _cts = new CancellationTokenSource();
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            }

            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts;

            public int Port { get; }

            public async Task<Dictionary<string, string>> GetQueryParamsAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                var ct = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;
                using (TcpClient client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false))
                {
                    try
                    {
                        return await GetQueryParamsFromClientAsync(client, ct).ConfigureAwait(false);
                    }
                    catch (ServerException e)
                    {
                        Logger.Warning("{0}", e.Message);
                        throw;
                    }
                }
            }

            private async Task<Dictionary<string, string>> GetQueryParamsFromClientAsync(TcpClient client, CancellationToken cancellationToken)
            {
                var stream = client.GetStream();

                var buffer = new byte[NetworkReadBufferSize];
                int bufferOfs = 0;
                int bufferSize = 0;
                Func<Task<char?>> getChar = async () =>
                {
                    if (bufferOfs == bufferSize)
                    {
                        bufferSize = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        if (bufferSize == 0)
                        {
                            // End of stream
                            return null;
                        }
                        bufferOfs = 0;
                    }
                    byte b = buffer[bufferOfs++];
                    // HTTP headers are generally ASCII, but historically allowed ISO-8859-1.
                    // Non-ASCII bytes should be treated opaquely, not further processed (e.g. as UTF8).
                    return (char)b;
                };

                string requestLine = await ReadRequestLine(getChar).ConfigureAwait(false);
                var requestParams = ValidateAndGetRequestParams(requestLine);
                await WaitForAllHeaders(getChar).ConfigureAwait(false);
                await WriteResponse(stream, cancellationToken).ConfigureAwait(false);

                return requestParams;
            }

            private async Task<string> ReadRequestLine(Func<Task<char?>> getChar)
            {
                var requestLine = new StringBuilder(MaxRequestLineLength);
                do
                {
                    if (requestLine.Length >= MaxRequestLineLength)
                    {
                        throw new ServerException($"Request line too long: > {MaxRequestLineLength} bytes.");
                    }
                    char? c = await getChar().ConfigureAwait(false);
                    if (c == null)
                    {
                        throw new ServerException("Unexpected end of network stream reading request line.");
                    }
                    requestLine.Append(c);
                } while (requestLine.Length < 2 || requestLine[requestLine.Length - 2] != '\r' || requestLine[requestLine.Length - 1] != '\n');
                requestLine.Length -= 2; // Remove \r\n
                return requestLine.ToString();
            }

            private Dictionary<string, string> ValidateAndGetRequestParams(string requestLine)
            {
                var requestLineParts = requestLine.Split(' ');
                if (requestLineParts.Length != 3)
                {
                    throw new ServerException("Request line ill-formatted. Should be '<request-method> <request-path> HTTP/1.1'");
                }
                string requestVerb = requestLineParts[0];
                if (requestVerb != "GET")
                {
                    throw new ServerException($"Expected 'GET' request, got '{requestVerb}'");
                }
                string requestPath = requestLineParts[1];
                if (!requestPath.StartsWith(LoopbackCallbackPath))
                {
                    throw new ServerException($"Expected request path to start '{LoopbackCallbackPath}', got '{requestPath}'");
                }
                var pathParts = requestPath.Split('?');
                if (pathParts.Length == 1)
                {
                    return new Dictionary<string, string>();
                }
                if (pathParts.Length != 2)
                {
                    throw new ServerException($"Expected a single '?' in request path, got '{requestPath}'");
                }
                var queryParams = pathParts[1];
                var result = queryParams.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries).Select(param =>
                {
                    var keyValue = param.Split('=');
                    if (keyValue.Length > 2)
                    {
                        throw new ServerException($"Invalid query parameter: '{param}'");
                    }
                    var key = WebUtility.UrlDecode(keyValue[0]);
                    var value = keyValue.Length == 2 ? WebUtility.UrlDecode(keyValue[1]) : "";
                    return new { key, value };
                }).ToDictionary(x => x.key, x => x.value);
                return result;
            }

            private async Task WaitForAllHeaders(Func<Task<char?>> getChar)
            {
                // Looking for an empty line, terminated by \r\n
                int byteCount = 0;
                int lineLength = 0;
                char c0 = '\0';
                char c1 = '\0';
                while (true)
                {
                    if (byteCount > MaxHeadersLength)
                    {
                        throw new ServerException($"Headers too long: > {MaxHeadersLength} bytes.");
                    }
                    char? c = await getChar().ConfigureAwait(false);
                    if (c == null)
                    {
                        throw new ServerException("Unexpected end of network stream waiting for headers.");
                    }
                    c0 = c1;
                    c1 = (char)c;
                    lineLength += 1;
                    byteCount += 1;
                    if (c0 == '\r' && c1 == '\n')
                    {
                        // End of line
                        if (lineLength == 2)
                        {
                            return;
                        }
                        lineLength = 0;
                    }
                }
            }

            private async Task WriteResponse(NetworkStream stream, CancellationToken cancellationToken)
            {
                string fullResponse = $"HTTP/1.1 200 OK\r\n\r\n{ClosePageResponse}";
                var response = Encoding.ASCII.GetBytes(fullResponse);
                await stream.WriteAsync(response, 0, response.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            public void Dispose()
            {
                _cts.Cancel();
                _listener.Stop();
            }
        }

        // There is a race condition on the port used for the loopback callback.
        // This is not good, but is now difficult to change due to RedirecrUri and ReceiveCodeAsync
        // being public methods.

        private string redirectUri;
        /// <inheritdoc />
        public string RedirectUri
        {
            get
            {
                if (!string.IsNullOrEmpty(redirectUri))
                {
                    return redirectUri;
                }

                return redirectUri = string.Format(LoopbackCallback, GetRandomUnusedPort());
            }
        }

        /// <inheritdoc />
        public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
            CancellationToken taskCancellationToken)
        {
            var authorizationUrl = url.Build().ToString();
            using (var listener = StartListener())
            {
                Logger.Debug("Open a browser with \"{0}\" URL", authorizationUrl);
                bool browserOpenedOk;
                try
                {
                    browserOpenedOk = OpenBrowser(authorizationUrl);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to launch browser with \"{0}\" for authorization", authorizationUrl);
                    throw new NotSupportedException(
                        $"Failed to launch browser with \"{authorizationUrl}\" for authorization. See inner exception for details.", e);
                }
                if (!browserOpenedOk)
                {
                    Logger.Error("Failed to launch browser with \"{0}\" for authorization; platform not supported.", authorizationUrl);
                    throw new NotSupportedException(
                        $"Failed to launch browser with \"{authorizationUrl}\" for authorization; platform not supported.");
                }

                // TODO: Use taskCancellationToken
                return await GetResponseFromListener(listener).ConfigureAwait(false);
            }
        }

        /// <summary>Returns a random, unused port.</summary>
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

#if NETSTANDARD
        private LimitedLocalhostHttpServer StartListener() => LimitedLocalhostHttpServer.Start(RedirectUri);

        private async Task<AuthorizationCodeResponseUrl> GetResponseFromListener(LimitedLocalhostHttpServer server)
        {
            var queryParams = await server.GetQueryParamsAsync().ConfigureAwait(false);

            // Create a new response URL with a dictionary that contains all the response query parameters.
            return new AuthorizationCodeResponseUrl(queryParams);
        }

        private bool OpenBrowser(string url)
        {
            // See https://github.com/dotnet/corefx/issues/10361
            // This is best-effort only, but should work most of the time.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
                return true;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
                return true;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
                return true;
            }
            return false;
        }
#else
        private HttpListener StartListener()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();
            return listener;
        }

        private async Task<AuthorizationCodeResponseUrl> GetResponseFromListener(HttpListener listener)
        {
            // Wait to get the authorization code response.
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            NameValueCollection coll = context.Request.QueryString;

            // Write a "close" response.
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.WriteLine(ClosePageResponse);
                writer.Flush();
            }
            context.Response.OutputStream.Close();

            // Create a new response URL with a dictionary that contains all the response query parameters.
            return new AuthorizationCodeResponseUrl(coll.AllKeys.ToDictionary(k => k, k => coll[k]));
        }

        private bool OpenBrowser(string url)
        {
            Process.Start(url);
            return true;
        }
#endif
    }
}