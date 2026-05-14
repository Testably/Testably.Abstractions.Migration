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
		INamedTypeSymbol mockFileData,
		INamedTypeSymbol mockDriveData,
		INamedTypeSymbol mockFileDataAccessor)
	{
		MockFileSystem = mockFileSystem;
		MockFileData = mockFileData;
		MockDriveData = mockDriveData;
		MockFileDataAccessor = mockFileDataAccessor;
	}

	public INamedTypeSymbol MockFileSystem { get; }
	public INamedTypeSymbol MockFileData { get; }
	public INamedTypeSymbol MockDriveData { get; }
	public INamedTypeSymbol MockFileDataAccessor { get; }

	public static TestableIoSymbols? TryGetFrom(Compilation compilation)
	{
		INamedTypeSymbol? mockFileSystem =
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockFileSystem");
		if (mockFileSystem is null)
		{
			return null;
		}

		INamedTypeSymbol? mockFileData =
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockFileData");
		INamedTypeSymbol? mockDriveData =
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".MockDriveData");
		INamedTypeSymbol? mockFileDataAccessor =
			compilation.GetTypeByMetadataName(TestingHelpersNamespace + ".IMockFileDataAccessor");

		if (mockFileData is null || mockDriveData is null || mockFileDataAccessor is null)
		{
			return null;
		}

		return new TestableIoSymbols(mockFileSystem, mockFileData, mockDriveData, mockFileDataAccessor);
	}
}
