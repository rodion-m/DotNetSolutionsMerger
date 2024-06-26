using System.Reflection;
using Microsoft.Build.Construction;

namespace DotNetSolutionsMerger
{
    public class SolutionMerger
    {
        private readonly List<string> _solutionPaths;
        private readonly string _outputPath;
        private readonly List<ProjectInSolution> _mergedProjects;
        private readonly HashSet<string> _solutionConfigurations;
        private readonly Dictionary<string, Dictionary<string, ProjectConfigurationInSolution>> _projectConfigurations;
        private readonly Dictionary<string, ProjectInSolution> _solutionFolders;
        private readonly Dictionary<string, List<string>> _nestedProjects;
        private readonly HashSet<string> _usedProjectGuids;

        private const string VbProjectGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        private const string CsProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private const string CpsProjectGuid = "{13B669BE-BB05-4DDF-9536-439F39A36129}";
        private const string CpsCsProjectGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        private const string CpsVbProjectGuid = "{778DAE3C-4631-46EA-AA77-85C1314464D9}";
        private const string CpsFsProjectGuid = "{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}";
        private const string VjProjectGuid = "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}";
        private const string VcProjectGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const string FsProjectGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        private const string DbProjectGuid = "{C8D11400-126E-41CD-887F-60BD40844F9E}";
        private const string WdProjectGuid = "{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}";
        private const string SynProjectGuid = "{BBD0F5D1-1CC4-42FD-BA4C-A96779C64378}";
        private const string WebProjectGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        private const string SolutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private const string SharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        private static readonly Dictionary<SolutionProjectType, string> ProjectTypeGuids =
            new Dictionary<SolutionProjectType, string>
            {
                { SolutionProjectType.SolutionFolder, SolutionFolderGuid },
                { SolutionProjectType.KnownToBeMSBuildFormat, CsProjectGuid },
                { SolutionProjectType.WebProject, WebProjectGuid },
                { SolutionProjectType.SharedProject, SharedProjectGuid }
            };

        private void Log(string message)
        {
            Console.WriteLine($"[SolutionMerger] {message}");
        }

        public SolutionMerger(IEnumerable<string> solutionPaths, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(solutionPaths);
            _outputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
            _solutionPaths = new List<string>(solutionPaths);
            _mergedProjects = new List<ProjectInSolution>();
            _solutionConfigurations = new HashSet<string>();
            _projectConfigurations = new Dictionary<string, Dictionary<string, ProjectConfigurationInSolution>>(StringComparer.OrdinalIgnoreCase);
            _solutionFolders = new Dictionary<string, ProjectInSolution>();
            _nestedProjects = new Dictionary<string, List<string>>();
            _usedProjectGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void MergeSolutions()
        {
            Log("Starting to merge solutions.");
            foreach (string solutionPath in _solutionPaths)
            {
                MergeSolution(solutionPath);
            }

            Log("Writing merged solution.");
            WriteMergedSolution();
            Log("Finished merging solutions.");
        }

        private void MergeSolution(string solutionPath)
        {
            Log($"Merging solution: {solutionPath}");
            SolutionFile solutionFile = SolutionFile.Parse(solutionPath);
            string solutionDirectory = Path.GetDirectoryName(solutionPath);
            string solutionName = Path.GetFileNameWithoutExtension(solutionPath);

            // Create a solution folder for this solution
            ProjectInSolution solutionFolder = CreateSolutionFolder(solutionFile, solutionName);
            ProjectInSolution sharedFolder = CreateSolutionFolder(solutionFile, "Shared");

            foreach (ProjectInSolution project in solutionFile.ProjectsInOrder)
            {
                if (project.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    continue; // Ignore solution folders from input solutions
                }

                string absolutePath = Path.GetFullPath(Path.Combine(solutionDirectory, project.RelativePath));
                string relativePath = GetRelativePath(absolutePath);

                if (File.Exists(absolutePath))
                {
                    // Check if a project with the same relative path already exists
                    Log($"Adding new project: {relativePath}");
                    ProjectInSolution existingProject = _mergedProjects.FirstOrDefault(p =>
                        p.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

                    if (existingProject == null)
                    {
                        // Add new project
                        SetRelativePath(project, relativePath);
                        EnsureUniqueProjectGuid(project);
                        _mergedProjects.Add(project);

                        // Add project to the appropriate solution folder
                        if (relativePath.Contains("shared", StringComparison.OrdinalIgnoreCase))
                        {
                            AddProjectToSolutionFolder(sharedFolder, project);
                        }
                        else
                        {
                            AddProjectToSolutionFolder(solutionFolder, project);
                        }
                    }
                    else
                    {
                        // Update existing project if necessary
                        Log($"Updating existing project: {relativePath}");
                        UpdateExistingProject(existingProject, project);
                    }
                }
                else
                {
                    Log($"Warning: Project file '{absolutePath}' not found. Skipping this project.");
                }
            }

            foreach (SolutionConfigurationInSolution? config in solutionFile.SolutionConfigurations)
            {
                _solutionConfigurations.Add(config.FullName);
            }

            foreach (ProjectInSolution? project in solutionFile.ProjectsInOrder)
            {
                if (!_projectConfigurations.TryGetValue(project.ProjectGuid,
                        out Dictionary<string, ProjectConfigurationInSolution>? configurations))
                {
                    configurations = new Dictionary<string, ProjectConfigurationInSolution>();
                    _projectConfigurations[project.ProjectGuid] = configurations;
                }

                foreach (KeyValuePair<string, ProjectConfigurationInSolution> config in project.ProjectConfigurations)
                {
                    configurations[config.Key] = config.Value;
                }
            }
        }

        private void EnsureUniqueProjectGuid(ProjectInSolution project)
        {
            if (_usedProjectGuids.Contains(project.ProjectGuid))
            {
                string newGuid = Guid.NewGuid().ToString("B").ToUpper();
                SetProjectGuid(project, newGuid);
            }

            _usedProjectGuids.Add(project.ProjectGuid);
        }

        private void SetProjectGuid(ProjectInSolution project, string newGuid)
        {
            PropertyInfo? projectGuidProperty =
                typeof(ProjectInSolution).GetProperty("ProjectGuid", BindingFlags.Public | BindingFlags.Instance);
            projectGuidProperty?.SetValue(project, newGuid);
        }

        private void UpdateExistingProject(ProjectInSolution existingProject, ProjectInSolution newProject)
        {
            // Update project configurations
            if (_projectConfigurations.TryGetValue(newProject.ProjectGuid,
                    out Dictionary<string, ProjectConfigurationInSolution>? newConfigurations))
            {
                if (!_projectConfigurations.TryGetValue(existingProject.ProjectGuid,
                        out Dictionary<string, ProjectConfigurationInSolution>? existingConfigurations))
                {
                    existingConfigurations = new Dictionary<string, ProjectConfigurationInSolution>();
                    _projectConfigurations[existingProject.ProjectGuid] = existingConfigurations;
                }

                foreach (KeyValuePair<string, ProjectConfigurationInSolution> config in newConfigurations)
                {
                    if (!existingConfigurations.ContainsKey(config.Key))
                    {
                        existingConfigurations[config.Key] = config.Value;
                    }
                }
            }

            // Update nested projects
            foreach (string folderGuid in _nestedProjects.Keys.ToList())
            {
                if (_nestedProjects[folderGuid].Contains(newProject.ProjectGuid))
                {
                    if (!_nestedProjects[folderGuid].Contains(existingProject.ProjectGuid))
                    {
                        _nestedProjects[folderGuid].Add(existingProject.ProjectGuid);
                    }

                    _nestedProjects[folderGuid].Remove(newProject.ProjectGuid);
                }
            }
        }

        /// <summary>
        /// Important:
        /// By design, this method uses reflection to create a new instance of ProjectInSolution and add it to the list of merged projects.
        /// </summary>
        private ProjectInSolution CreateSolutionFolder(SolutionFile parentSolutionFile, string folderName)
        {
            if (_solutionFolders.TryGetValue(folderName, out ProjectInSolution? existingFolder))
            {
                return existingFolder;
            }

            // Constructor of ProjectInSolution and its properties are internal, so we need to use reflection to create a new instance
            ConstructorInfo? constructorInfo = typeof(ProjectInSolution).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(SolutionFile) },
                null);

            if (constructorInfo is null)
            {
                Log("Failed to find constructor for ProjectInSolution.");
                throw new InvalidOperationException(
                    "Failed to find constructor for ProjectInSolution. Ensure that MSBuild version is 17.10.4.");
            }

            ProjectInSolution folder = (ProjectInSolution)constructorInfo.Invoke(new object[] { parentSolutionFile });

            PropertyInfo? projectNameProperty =
                typeof(ProjectInSolution).GetProperty("ProjectName", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? projectGuidProperty =
                typeof(ProjectInSolution).GetProperty("ProjectGuid", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? relativePathProperty =
                typeof(ProjectInSolution).GetProperty("RelativePath", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? projectTypeProperty =
                typeof(ProjectInSolution).GetProperty("ProjectType", BindingFlags.Public | BindingFlags.Instance);

            projectNameProperty!.SetValue(folder, folderName);
            projectGuidProperty!.SetValue(folder, Guid.NewGuid().ToString("B").ToUpper());
            relativePathProperty!.SetValue(folder, folderName);
            projectTypeProperty!.SetValue(folder, SolutionProjectType.SolutionFolder);

            _solutionFolders[folderName] = folder;
            _mergedProjects.Add(folder);
            return folder;
        }

        private void AddProjectToSolutionFolder(ProjectInSolution folder, ProjectInSolution project)
        {
            if (!_nestedProjects.TryGetValue(folder.ProjectGuid, out List<string>? nestedProjects))
            {
                nestedProjects = new List<string>();
                _nestedProjects[folder.ProjectGuid] = nestedProjects;
            }

            nestedProjects.Add(project.ProjectGuid);
        }

        private void WriteMergedSolution()
        {
            using StreamWriter writer = new StreamWriter(_outputPath);
            writer.WriteLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            writer.WriteLine("# Visual Studio Version 17");
            writer.WriteLine("VisualStudioVersion = 17.0.31903.59");
            writer.WriteLine("MinimumVisualStudioVersion = 10.0.40219.1");

            foreach (ProjectInSolution project in _mergedProjects)
            {
                string projectTypeGuid = GetProjectTypeGuid(project);
                writer.WriteLine(
                    $"Project(\"{projectTypeGuid}\") = \"{project.ProjectName}\", \"{project.RelativePath}\", \"{project.ProjectGuid}\"");
                writer.WriteLine("EndProject");
            }

            writer.WriteLine("Global");

            writer.WriteLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            foreach (string config in _solutionConfigurations)
            {
                writer.WriteLine($"\t\t{config} = {config}");
            }

            writer.WriteLine("\tEndGlobalSection");

            writer.WriteLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            foreach (ProjectInSolution project in _mergedProjects.Where(p =>
                         p.ProjectType != SolutionProjectType.SolutionFolder))
            {
                if (_projectConfigurations.TryGetValue(project.ProjectGuid,
                        out Dictionary<string, ProjectConfigurationInSolution>? configurations))
                {
                    foreach (KeyValuePair<string, ProjectConfigurationInSolution> config in configurations)
                    {
                        writer.WriteLine(
                            $"\t\t{project.ProjectGuid}.{config.Key}.ActiveCfg = {config.Value.FullName}");
                        writer.WriteLine(
                            $"\t\t{project.ProjectGuid}.{config.Key}.Build.0 = {config.Value.FullName}");
                    }
                }
            }

            writer.WriteLine("\tEndGlobalSection");

            writer.WriteLine("\tGlobalSection(SolutionProperties) = preSolution");
            writer.WriteLine("\t\tHideSolutionNode = FALSE");
            writer.WriteLine("\tEndGlobalSection");

            writer.WriteLine("\tGlobalSection(NestedProjects) = preSolution");
            foreach (KeyValuePair<string, List<string>> folder in _nestedProjects)
            {
                foreach (string projectGuid in folder.Value)
                {
                    writer.WriteLine($"\t\t{projectGuid} = {folder.Key}");
                }
            }

            writer.WriteLine("\tEndGlobalSection");

            writer.WriteLine("EndGlobal");
        }

        private string GetProjectTypeGuid(ProjectInSolution project)
        {
            if (ProjectTypeGuids.TryGetValue(project.ProjectType, out string? guid))
            {
                return guid;
            }

            // If not found in the dictionary, try to determine based on file extension
            string extension = Path.GetExtension(project.RelativePath).ToLowerInvariant();
            switch (extension)
            {
                case ".csproj":
                    return project.RelativePath.Contains("Microsoft.NET.Sdk") ? CpsCsProjectGuid : CsProjectGuid;
                case ".vbproj":
                    return project.RelativePath.Contains("Microsoft.NET.Sdk") ? CpsVbProjectGuid : VbProjectGuid;
                case ".fsproj":
                    return project.RelativePath.Contains("Microsoft.NET.Sdk") ? CpsFsProjectGuid : FsProjectGuid;
                case ".vcxproj":
                    return VcProjectGuid;
                case ".dbproj":
                    return DbProjectGuid;
                case ".wdproj":
                    return WdProjectGuid;
                case ".synproj":
                    return SynProjectGuid;
                default:
                    throw new InvalidOperationException($"Unknown project type for project '{project.RelativePath}'.");
            }
        }

        private string GetRelativePath(string absolutePath)
        {
            string? outputDirectory = Path.GetDirectoryName(_outputPath);
            string relativePath = Path.GetRelativePath(outputDirectory, absolutePath);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private void SetRelativePath(ProjectInSolution project, string relativePath)
        {
            PropertyInfo? propertyInfo =
                typeof(ProjectInSolution).GetProperty(nameof(ProjectInSolution.RelativePath), BindingFlags.Public | BindingFlags.Instance);
            
            if (propertyInfo is null)
            {
                Log("Failed to find property 'RelativePath' in ProjectInSolution.");
                throw new InvalidOperationException("Failed to find property 'RelativePath' in ProjectInSolution.");
            }
            
            propertyInfo.SetValue(project, relativePath);
        }
    }
}
