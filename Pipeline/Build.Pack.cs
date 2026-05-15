using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Serilog.Log;

// ReSharper disable AllUnderscoreLocalParameterName

namespace Build;

partial class Build
{
	Target UpdateReadme => _ => _
		.DependsOn(Clean)
		.Before(Compile)
		.Executes(() =>
		{
			string version = string.Join('.', GitVersion.SemVer.Split('.').Take(3));
			if (version.IndexOf('-') != -1)
			{
				version = version.Substring(0, version.IndexOf('-'));
			}

			StringBuilder sb = new();
			string[] lines = File.ReadAllLines(Solution.Directory / "README.md");
			sb.AppendLine(lines.First());
			sb.AppendLine(
				$"[![Changelog](https://img.shields.io/badge/Changelog-v{version}-blue)](https://github.com/Testably/Testably.Abstractions.Migration/releases/tag/v{version})");
			foreach (string line in lines.Skip(1))
			{
				if (line.StartsWith("[![Build](https://github.com/Testably/Testably.Abstractions.Migration/actions/workflows/build.yml") ||
				    line.StartsWith("[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure"))
				{
					continue;
				}

				if (line.StartsWith("[![Coverage](https://sonarcloud.io/api/project_badges/measure"))
				{
					sb.AppendLine(line
						.Replace(")", $"&branch=release/v{version})"));
					continue;
				}

				sb.AppendLine(line);
			}

			File.WriteAllText(ArtifactsDirectory / "README.md", sb.ToString());
		});

	Target Pack => _ => _
		.DependsOn(UpdateReadme)
		.DependsOn(Compile)
		.Executes(() =>
		{
			AbsolutePath packagesDirectory = ArtifactsDirectory / "Packages";
			packagesDirectory.CreateOrCleanDirectory();

			// Pack the meta-package separately from the slnx build. The meta-package
			// csproj disables GeneratePackageOnBuild because adding ProjectReferences
			// to the analyzer projects from the meta-package was found to create a
			// duplicate MSBuild graph node for those analyzers under parallel CI
			// builds, racing on bin/.../deps.json files and failing GenerateDepsFile
			// with "file in use". Packing here with NoBuild after Compile sidesteps
			// the race entirely: the slnx build has already produced every analyzer
			// DLL the meta-package needs to bundle into analyzers/dotnet/cs/. Pack
			// runs against the .slnx (not the csproj) so $(SolutionDir) resolves for
			// the README pack target; only the meta-package is IsPackable=true so it
			// is the only project actually packed.
			DotNetPack(s => s
				.SetProject(Solution.Path)
				.SetConfiguration(Configuration)
				.EnableNoLogo()
				.EnableNoRestore()
				.EnableNoBuild()
				.SetVersion(MainVersion.FileVersion + MainVersion.PreRelease)
				.SetAssemblyVersion(MainVersion.FileVersion)
				.SetFileVersion(MainVersion.FileVersion)
				.SetInformationalVersion(MainVersion.InformationalVersion));

			List<string> packages = new();
			foreach (Project project in new[]
			         {
				         Solution.Testably_Abstractions_Migration,
			         })
			{
				foreach (string package in
				         Directory.EnumerateFiles(project.Directory / "bin", "*.nupkg", SearchOption.AllDirectories))
				{
					File.Move(package, packagesDirectory / Path.GetFileName(package));
					Debug("Found nuget package: {PackagePath}", package);
					packages.Add(Path.GetFileName(package));
				}

				foreach (string symbolPackage in
				         Directory.EnumerateFiles(project.Directory / "bin", "*.snupkg", SearchOption.AllDirectories))
				{
					File.Move(symbolPackage, packagesDirectory / Path.GetFileName(symbolPackage));
					Debug("Found symbol package: {PackagePath}", symbolPackage);
				}
			}

			ReportSummary(s => s
				.AddPair("Packages", string.Join(", ", packages)));
		});
}
