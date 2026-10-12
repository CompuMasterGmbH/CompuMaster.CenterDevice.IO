using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    [TestFixture, Category("Isolated")]
    public class ParallelCleanupGuardTests
    {
        [TestCase("hidden"), TestCase("external"), TestCase("empty"), TestCase("missing-current"), TestCase("omitted"), TestCase("null-document")]
        public void UnverifiedCollectionReferencesRejectBeforeAnyDestructiveCallback(string problem)
        {
            var document = new DocumentFullMetadata { Collections = new SharingInfo { Visible = new List<string> { "first", "second" } } };
            if (problem == "hidden") document.Collections.NotVisibleCount = 1;
            if (problem == "external") document.Collections.Visible.Add("unowned");
            if (problem == "empty") document.Collections.Visible.Clear();
            if (problem == "missing-current") document.Collections.Visible.Remove("first");
            if (problem == "omitted") document.Collections = null;
            if (problem == "null-document") document = null;
            var erased = false;
            Assert.Catch<Exception>((Action)(() =>
            {
                DocumentVersionBridgeTests.RequireOwnedCollectionReferences(document, "first", new[] { "first", "second" });
                erased = true;
            }));
            Assert.That(erased, Is.False);
        }

        [Test]
        public void TwoVerifiedOwnedCollectionReferencesPermitCleanupInEitherScope()
        {
            var document = new DocumentFullMetadata { Collections = new SharingInfo { Visible = new List<string> { "second", "first" } } };
            DocumentVersionBridgeTests.RequireOwnedCollectionReferences(document, "first", new[] { "first", "second" });
            DocumentVersionBridgeTests.RequireOwnedCollectionReferences(document, "second", new[] { "first", "second" });
        }
    }
}
