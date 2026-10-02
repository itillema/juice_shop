using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Utility.Configuration;

/// <summary>Validates <see cref="AutomationSettings"/> and every section nested inside it.</summary>
/// <remarks>
/// Replaces <c>ValidateDataAnnotations()</c>, which validates the root object only — it runs <see cref="Validator"/> once against <see cref="AutomationSettings"/> and does not recurse.
/// The root's <c>[Required]</c> attributes passed because every section is initialised to a new instance, so every <c>[Range]</c> and <c>[RegularExpression]</c> on the sections was dead: <c>AUTOMATION__BROWSER__NAME=safari</c> bound happily and surfaced much later as a Playwright error inside the first test, which is the opposite of what validation is for.
/// Sections are found by reflection rather than listed by hand. A section added later is then validated without anyone remembering to come back here — being forgotten is the failure this type exists to prevent.
/// </remarks>
internal sealed class AutomationSettingsValidator : IValidateOptions<AutomationSettings>
{
    private static readonly string SectionNamespace = typeof(AutomationSettings).Namespace!;

    /// <summary>Validates the whole settings tree, reporting every property failure at once.</summary>
    public ValidateOptionsResult Validate(string? name, AutomationSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        Validate(
            options,
            AutomationSettings.SectionName,
            failures,
            new HashSet<object>(ReferenceEqualityComparer.Instance));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <param name="path">Configuration key path of <paramref name="instance"/>, e.g. <c>Automation:Browser</c>.</param>
    /// <param name="visited">Guards against a settings graph that ever gains a cycle.</param>
    private static void Validate(object instance, string path, List<string> failures, HashSet<object> visited)
    {
        if (!visited.Add(instance))
        {
            return;
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true);

        failures.AddRange(results.Select(result => Describe(result, path)));

        foreach (var section in NestedSections(instance))
        {
            Validate(section.Value, $"{path}:{section.Key}", failures, visited);
        }
    }

    /// <summary>Names the configuration key, which is what the reader has to go and change.</summary>
    /// <remarks>
    /// A result that names no member leaves the section path standing alone. Environment overrides spell the same key with a double underscore.
    /// </remarks>
    private static string Describe(ValidationResult result, string path)
    {
        var members = string.Join(", ", result.MemberNames);

        return string.IsNullOrEmpty(members)
            ? $"{path}: {result.ErrorMessage}"
            : $"{path}:{members}: {result.ErrorMessage}";
    }

    /// <summary>True for a type declared in the settings namespace, or anywhere below it.</summary>
    /// <remarks>
    /// The subtree, not the one namespace: a section filed under <c>Configuration.Browser</c> would otherwise be skipped in silence, which is the very thing the reflective walk exists to stop.
    /// </remarks>
    private static bool IsSection(Type type) =>
        type.Namespace is { } candidate
        && (candidate.Equals(SectionNamespace, StringComparison.Ordinal)
            || candidate.StartsWith($"{SectionNamespace}.", StringComparison.Ordinal));

    /// <summary>The settings objects hanging off <paramref name="instance"/>, by property name.</summary>
    /// <remarks>
    /// Restricted to our own settings types, which is what distinguishes a section from a value: <c>string</c> and <c>int</c> are covered by the attributes on them, and walking into the BCL  — <see cref="BrowserSettings.SessionCookies"/> included — would recurse without end. Nothing validates an individual cookie entry.
    /// </remarks>
    private static IEnumerable<KeyValuePair<string, object>> NestedSections(object instance)
    {
        foreach (var property in instance.GetType().GetProperties())
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (!IsSection(property.PropertyType))
            {
                continue;
            }

            if (property.GetValue(instance) is { } section)
            {
                yield return new KeyValuePair<string, object>(property.Name, section);
            }
        }
    }
}
