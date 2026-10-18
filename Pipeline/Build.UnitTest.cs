using System.Linq;
using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// ReSharper disable AllUnderscoreLocalParameterName

namespace Build;

partial class Build
{
	Target UnitTests => _ => _
		.DependsOn(DotNetUnitTests);

	Project[] UnitTestProjects =>
	[
		Solution.Playground.Testably_Abstractions_Migration_SystemIOAbstractionsPlayground,
		Solution.Tests.Testably_Abstractions_Migration_Example_Tests,
		Solution.Tests.Testably_Abstractions_Migration_Tests,
	];

	Target DotNetUnitTests => _ => _
		.Unlisted()
		.DependsOn(Compile)
		.Executes(() =>
		{
			string[] excludedFrameworks =
				EnvironmentInfo.IsWin
					? []
					: ["net48",];
			(Project Project, string Framework)[] testRuns = UnitTestProjects
				.SelectMany(project => (project.GetTargetFrameworks() ?? [])
					.Except(excludedFrameworks)
					.Select(framework => (project, framework)))
				.ToArray();

			Assert.NotEmpty(testRuns);

			// global.json opts into the Microsoft.Testing.Platform, so `dotnet test` takes the project with `--project`.
			DotNetTest(s => s
					.SetConfiguration(Configuration)
					.SetProcessEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
					.EnableNoBuild()
					.SetResultsDirectory(TestResultsDirectory)
					.CombineWith(testRuns,
						(settings, testRun) => settings
							.SetFramework(testRun.Framework)
							.AddProcessAdditionalArguments(
								$"--project \"{testRun.Project.Path}\"",
								"--report-trx",
								$"--report-trx-filename {testRun.Project.Name}_{testRun.Framework}.trx",
								"--coverage",
								"--coverage-output-format cobertura",
								$"--coverage-output {testRun.Project.Name}_{testRun.Framework}.cobertura.xml")),
				completeOnFailure: true);
		});
}
