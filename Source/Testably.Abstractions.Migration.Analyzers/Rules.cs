using Microsoft.CodeAnalysis;

namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     The rules for the analyzers in this project.
/// </summary>
public static class Rules
{
	private const string UsageCategory = "Usage";

	private const string DocsBaseUrl =
		"https://docs.testably.org/Abstractions/Migration";

	/// <summary>
	///     Migration rule for <c>System.IO.Abstractions.TestingHelpers</c> usage. Flags any usage of
	///     <c>new MockFileSystem(...)</c>, <c>new MockFileData(...)</c> or the <c>IMockFileDataAccessor</c>
	///     API that should be migrated to <c>Testably.Abstractions.Testing.MockFileSystem</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor SystemIOAbstractionsRule =
		CreateDescriptor("TestablyM001", UsageCategory, DiagnosticSeverity.Warning);


	private static DiagnosticDescriptor CreateDescriptor(string diagnosticId, string category,
		DiagnosticSeverity severity) => new(
		diagnosticId,
		new LocalizableResourceString(diagnosticId + "Title",
			Resources.ResourceManager, typeof(Resources)),
		new LocalizableResourceString(diagnosticId + "MessageFormat", Resources.ResourceManager,
			typeof(Resources)),
		category,
		severity,
		true,
		new LocalizableResourceString(diagnosticId + "Description", Resources.ResourceManager,
			typeof(Resources)),
		helpLinkUri: DocsBaseUrl
	);
}
