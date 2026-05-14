namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Discriminator values for the <c>pattern</c> property carried by every
///     <see cref="Rules.SystemIOAbstractionsRule" /> diagnostic. The accompanying code fix
///     dispatches on this value to pick the appropriate rewrite.
/// </summary>
public static class Patterns
{
	/// <summary>
	///     The key under which the pattern discriminator is stored in
	///     <see cref="Microsoft.CodeAnalysis.Diagnostic.Properties" />.
	/// </summary>
	public const string Key = "pattern";

	/// <summary>The parameterless <c>new MockFileSystem()</c> constructor.</summary>
	public const string MockFileSystemDefaultConstructor = "MockFileSystem.ctor()";
}
