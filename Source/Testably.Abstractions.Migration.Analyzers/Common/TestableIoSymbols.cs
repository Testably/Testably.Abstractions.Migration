using Microsoft.CodeAnalysis;

namespace Testably.Abstractions.Migration.Analyzers.Common;

/// <summary>
///     Caches the well-known <c>System.IO.Abstractions.TestingHelpers</c> type symbols for a
///     <see cref="Compilation" />. Returns <see langword="null" /> when the
///     <c>System.IO.Abstractions.TestingHelpers</c> assembly is not referenced so the analyzer
///     can bail out cheaply.
/// </summary>
internal sealed class TestableIoSymbols
{
	public const string TestingHelpersNamespace = "System.IO.Abstractions.TestingHelpers";

	private TestableIoSymbols(
		INamedTypeSymbol mockFileSystem,
		INamedTypeSymbol? mockFileData,
		INamedTypeSymbol? mockDriveData,
		INamedTypeSymbol? mockFileDataAccessor,
		INamedTypeSymbol? mockFileVersionInfo)
	{
		MockFileSystem = mockFileSystem;
		MockFileData = mockFileData;
		MockDriveData = mockDriveData;
		MockFileDataAccessor = mockFileDataAccessor;
		MockFileVersionInfo = mockFileVersionInfo;
	}

	public INamedTypeSymbol MockFileSystem { get; }

	// Auxiliary types are nullable: a future TestingHelpers rename or removal should
	// only disable the patterns that actually consume the missing type, not the whole
	// analyzer. Call sites that need one of these symbols must null-check first.
	public INamedTypeSymbol? MockFileData { get; }
	public INamedTypeSymbol? MockDriveData { get; }
	public INamedTypeSymbol? MockFileDataAccessor { get; }
	public INamedTypeSymbol? MockFileVersionInfo { get; }

	public static TestableIoSymbols? TryGetFrom(Compilation compilation)
	{
		INamedTypeSymbol? mockFileSystem =
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockFileSystem");
		if (mockFileSystem is null)
		{
			return null;
		}

		return new TestableIoSymbols(
			mockFileSystem,
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockFileData"),
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockDriveData"),
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".IMockFileDataAccessor"),
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockFileVersionInfo"));
	}
}
