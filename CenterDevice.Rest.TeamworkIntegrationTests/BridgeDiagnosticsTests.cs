using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        [TestCase(502), TestCase(503), Category("Isolated")]
        public void BridgeServerErrorDiagnosticsPreserveOnlyStructuredStatus(int status)
        {
            const string secret = "dummy-sensitive-response-or-token";
            var error = new WebException(secret, new HttpRequestException(secret), WebExceptionStatus.UnknownError, null);
            error.Data["CompuMaster.Scopevisio.Teamwork.HttpStatusCode"] = status;
            error.Data["unrelated-sensitive-field"] = secret;
            var diagnostic = Diagnostic(error);
            Assert.That(diagnostic, Does.Contain("HTTP " + status));
            Assert.That(diagnostic, Does.Contain("UnknownError"));
            Assert.That(diagnostic, Does.Contain("HttpRequestException"));
            Assert.That(diagnostic, Does.Not.Contain(secret));

            error.Data["CompuMaster.Scopevisio.Teamwork.HttpStatusCode"] = "503 " + secret;
            Assert.That(Diagnostic(error), Does.Not.Contain("HTTP 503"));
            Assert.That(Diagnostic(error), Does.Not.Contain(secret));
            error.Data["CompuMaster.Scopevisio.Teamwork.HttpStatusCode"] = 600;
            Assert.That(Diagnostic(error), Does.Not.Contain("HTTP 600"));
        }
    }
}
