using CenterDevice.Rest.Clients.OAuth;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class PublishedApiCompatibilityTests
    {
        [Test]
        public void PublishedPublicAndProtectedSignaturesAndDefaultsRemainAvailable()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "CompatibilityBaseline", "CompuMaster.CenterDevice.Rest.dll");
            Assert.That(File.Exists(path), Is.True, "Restore the pinned PackageDownload compatibility baseline.");
            var baseline = Assembly.LoadFile(Path.GetFullPath(path));
            var current = typeof(OAuthRestClient).Assembly;
            Assert.That(baseline.GetName().Version, Is.EqualTo(new Version(2026, 10, 9, 0)));
            var oldSurface = Surface(baseline);
            var newSurface = new HashSet<string>(Surface(current), StringComparer.Ordinal);
            var missing = oldSurface.Where(signature => !newSurface.Contains(signature)).OrderBy(s => s).ToArray();
            Assert.That(missing, Is.Empty, "Removed or changed published signatures/defaults:\n" + string.Join("\n", missing));
            Assert.That(newSurface.Count, Is.GreaterThan(oldSurface.Count()), "This change must be additive.");
        }

        private static IEnumerable<string> Surface(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(IsVisible))
            {
                var prefix = type.FullName + " ";
                yield return prefix + "type " + type.Attributes + " base=" + type.BaseType + " interfaces="
                    + string.Join(",", type.GetInterfaces().Select(t => t.ToString()).OrderBy(s => s));
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (member is MethodBase method && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly))
                    {
                        var returns = method is MethodInfo info ? info.ReturnType.ToString() : "constructor";
                        var parameters = method.GetParameters().Select(p => p.ParameterType + " " + p.Name + " " + p.Attributes
                            + " default=" + (p.HasDefaultValue ? Convert.ToString(p.DefaultValue, CultureInfo.InvariantCulture) : "<none>"));
                        var constraints = method.IsGenericMethod ? string.Join(";", method.GetGenericArguments().Select(p => p.GenericParameterAttributes + ":"
                            + string.Join(",", p.GetGenericParameterConstraints().Select(t => t.ToString()).OrderBy(s => s)))) : "";
                        yield return prefix + "method " + method.Attributes + " " + returns + " " + method.Name + "(" + string.Join(",", parameters) + ") " + constraints;
                    }
                    else if (member is FieldInfo field && (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly))
                        yield return prefix + "field " + field.Attributes + " " + field.FieldType + " " + field.Name
                            + (field.IsLiteral ? "=" + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture) : "");
                    else if (member is PropertyInfo property && property.GetAccessors(true).Any(m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly))
                        yield return prefix + "property " + property.PropertyType + " " + property.Name;
                    else if (member is EventInfo eventInfo && eventInfo.GetAddMethod(true) is MethodInfo add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly))
                        yield return prefix + "event " + eventInfo.EventHandlerType + " " + eventInfo.Name;
                }
            }
        }

        private static bool IsVisible(Type type)
        {
            if (!type.IsNested) return type.IsPublic;
            return (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) && IsVisible(type.DeclaringType);
        }
    }
}
