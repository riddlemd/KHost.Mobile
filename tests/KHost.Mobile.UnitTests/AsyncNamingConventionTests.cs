using System.Reflection;
using System.Runtime.CompilerServices;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Common.Json;
using Xunit;

namespace KHost.Mobile.UnitTests;

// Naming rules have no return-type predicate, so .editorconfig's async_methods_end_in_async keys off the `async`
// keyword and never sees a passthrough like `Task Foo() => Bar.InvokeAsync();`.
public sealed class AsyncNamingConventionTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(IAppSettings).Assembly,              // Abstractions
        typeof(JsonElementExtensions).Assembly,     // Common
        // Fully qualified: this project has its own Infrastructure/Clients namespaces that would win the lookup.
        typeof(global::KHost.Mobile.Infrastructure.Project).Assembly,
        typeof(global::KHost.Mobile.Clients.Project).Assembly,
        typeof(global::KHost.Mobile.UI.Project).Assembly,
    ];

    [Fact]
    public void Task_returning_methods_end_in_Async()
    {
        var offenders = Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !IsCompilerGenerated(t))
            // DeclaredOnly or every inherited ComponentBase/framework method gets flagged too.
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            // '<' catches local functions and async state machines, which reflection surfaces as real methods.
            .Where(m => !m.IsSpecialName && !IsCompilerGenerated(m) && !m.Name.Contains('<'))
            .Where(ReturnsTask)
            .Where(m => !m.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .Distinct()
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Task-returning methods must end in 'Async':{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    private static bool ReturnsTask(MethodInfo method)
    {
        var t = method.ReturnType;
        if (t == typeof(Task) || t == typeof(ValueTask)) return true;
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Task<>) || def == typeof(ValueTask<>);
    }

    private static bool IsCompilerGenerated(MemberInfo member)
        => member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
}
