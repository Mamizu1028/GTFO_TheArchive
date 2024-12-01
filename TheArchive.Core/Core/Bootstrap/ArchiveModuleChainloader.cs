using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TheArchive.Core.Attributes;
using TheArchive.Interfaces;

namespace TheArchive.Core.Bootstrap;

public class ArchiveModuleChainloader
{
    private static IArchiveLogger _logger;
    private static IArchiveLogger Logger => _logger ??= Loader.LoaderWrapper.CreateLoggerInstance(nameof(ArchiveModuleChainloader), ConsoleColor.White);

    private static Regex allowedGuidRegex { get; } = new Regex("^[a-zA-Z0-9\\._\\-]+$");

    public static ModuleInfo ToModuleInfo(TypeDefinition type, string assemblyLocation)
    {
        if (type.IsInterface || type.IsAbstract)
        {
            return null;
        }
        try
        {
            if (!type.Interfaces.Any(i => i.InterfaceType.FullName == typeof(IArchiveModule).FullName))
            {
                return null;
            }
        }
        catch (AssemblyResolutionException ex)
        {
            Logger.Exception(ex);
            return null;
        }
        ArchiveModule metadata = ArchiveModule.FromCecilType(type);
        if (metadata == null)
        {
            Logger.Warning($"Skipping over type [{type.FullName}] as no metadata attribute is specified.");
            return null;
        }
        if (string.IsNullOrEmpty(metadata.GUID) || !allowedGuidRegex.IsMatch(metadata.GUID))
        {
            Logger.Warning($"Skipping type [{type.FullName}] because its GUID [{metadata.GUID}] is of an illegal format.");
            return null;
        }
        if (metadata.Version == null)
        {
            Logger.Warning($"Skipping type [{type.FullName}] because its version is invalid.");
            return null;
        }
        if (metadata.Name == null)
        {
            Logger.Warning($"Skipping type [{type.FullName}] because its name is null.");
            return null;
        }
        IEnumerable<ArchiveDependency> dependencies = ArchiveDependency.FromCecilType(type);
        IEnumerable<ArchiveIncompatibility> incompatibilities = ArchiveIncompatibility.FromCecilType(type);
        AssemblyNameReference assemblyNameReference = type.Module.AssemblyReferences.FirstOrDefault((reference) => reference.Name == CurrentAssemblyName);
        System.Version coreVersion = (assemblyNameReference != null ? assemblyNameReference.Version : null) ?? new();
        return new ModuleInfo
        {
            Metadata = metadata,
            Dependencies = dependencies,
            Incompatibilities = incompatibilities,
            TypeName = type.FullName,
            TargettedTheArchiveVersion = coreVersion,
            Location = assemblyLocation
        };
    }

    protected static bool HasArchiveModule(AssemblyDefinition ass)
    {
        if (!ass.MainModule.AssemblyReferences.Any((r) => r.Name == CurrentAssemblyName))
        {
            return false;
        }
        var res = ass.MainModule.GetTypes().Any((td) => td.Interfaces.Any(p => p.InterfaceType.FullName == typeof(IArchiveModule).FullName));
        return res;
    }

    protected static bool ModuleTargetsWrongTheArchive(ModuleInfo ModuleInfo)
    {
        System.Version moduleTarget = ModuleInfo.TargettedTheArchiveVersion;
        return moduleTarget.Major != CurrentAssemblyVersion.Major || moduleTarget.Minor > CurrentAssemblyVersion.Minor || moduleTarget.Minor >= CurrentAssemblyVersion.Minor && moduleTarget.Build > CurrentAssemblyVersion.Build;
    }

    public Dictionary<string, ModuleInfo> Modules { get; } = new Dictionary<string, ModuleInfo>();

    public List<string> DependencyErrors { get; } = new List<string>();

    public event Action<ModuleInfo> ModuleLoaded;

    public event Action Finished;

    public static void Initialize()
    {
        if (Instance != null)
        {
            throw new InvalidOperationException("Chainloader cannot be initialized multiple times");
        }
        Instance = new();
        Logger.Notice("Chainloader initialized");
        Instance.Execute();
    }

    protected IList<ModuleInfo> DiscoverModulesFrom(string path, string cacheName = "TheArchive_ModuleChainloader")
    {
        return TypeLoader.FindModuleTypes(path, new Func<TypeDefinition, string, ModuleInfo>(ToModuleInfo), new Func<AssemblyDefinition, bool>(HasArchiveModule), cacheName).SelectMany((p) => p.Value).ToList();
    }

    protected IList<ModuleInfo> DiscoverModules()
    {
#if BepInEx
        return DiscoverModulesFrom(BepInEx.Paths.PluginPath, "TheArchive_ModuleChainloader");
#endif
    }

    protected IList<ModuleInfo> ModifyLoadOrder(IList<ModuleInfo> modules)
    {
        var dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
        var modulesByGuid = new Dictionary<string, ModuleInfo>();

        foreach (IGrouping<string, ModuleInfo> ModuleInfoGroup in from info in modules
                                                                  group info by info.Metadata.GUID)
        {
            if (Modules.TryGetValue(ModuleInfoGroup.Key, out var loadedModule))
            {
                Logger.Warning($"Skipping [{ModuleInfoGroup.Key}] because a module with a similar GUID ([{loadedModule}]) has been already loaded.");
                continue;
            }
            ModuleInfo loadedVersion = null;
            foreach (ModuleInfo moduleInfo in ModuleInfoGroup.OrderByDescending((x) => x.Metadata.Version))
            {
                if (loadedVersion != null)
                {
                    Logger.Warning($"Skip [{moduleInfo}] because a newer version exists ({loadedVersion})");
                    continue;
                }

                loadedVersion = moduleInfo;
                dependencyDict[moduleInfo.Metadata.GUID] = moduleInfo.Dependencies.Select((d) => d.DependencyGUID);
                modulesByGuid[moduleInfo.Metadata.GUID] = moduleInfo;
            }
        }


        foreach (var moduleInfo in modulesByGuid.Values.ToList())
        {
            if (moduleInfo.Incompatibilities.Any(incompatibility => modulesByGuid.ContainsKey(incompatibility.IncompatibilityGUID)
            || Modules.ContainsKey(incompatibility.IncompatibilityGUID)
#if BepInEx
                 || BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.ContainsKey(incompatibility.IncompatibilityGUID)
#endif
            ))
            {
                modulesByGuid.Remove(moduleInfo.Metadata.GUID);
                dependencyDict.Remove(moduleInfo.Metadata.GUID);

                var incompatiblePluginsNew = moduleInfo.Incompatibilities.Select(x => x.IncompatibilityGUID)
                                                       .Where(x => modulesByGuid.ContainsKey(x));
                var incompatiblePluginsExisting = moduleInfo.Incompatibilities.Select(x => x.IncompatibilityGUID)
                                                            .Where(x => Modules.ContainsKey(x));
                var incompatiblePlugins = incompatiblePluginsNew.Concat(incompatiblePluginsExisting).ToArray();
                var message =
                    $@"Could not load [{moduleInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
                DependencyErrors.Add(message);
                Logger.Error(message);
            }
            else if (ModuleTargetsWrongTheArchive(moduleInfo))
            {
                var message =
                    $@"Module [{moduleInfo}] targets a wrong version of BepInEx ({moduleInfo.TargettedTheArchiveVersion}) and might not work until you update";
                DependencyErrors.Add(message);
                Logger.Warning(message);
            }
        }


        var emptyDependencies = new string[0];

        var sortedModules = Utility.TopologicalSort(dependencyDict.Keys,
                                                    x =>
                                                        dependencyDict.TryGetValue(x, out var deps)
                                                            ? deps
                                                            : emptyDependencies).ToList();

        return sortedModules.Where(modulesByGuid.ContainsKey).Select(x => modulesByGuid[x]).ToList();
    }


    public void Execute()
    {
        try
        {
            IList<ModuleInfo> modules = DiscoverModules();
            Logger.Info($"{modules.Count} module{(modules.Count == 1 ? "" : "s")} to load");
            LoadModules(modules);
            Finished?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error("Error occurred loading modules:");
            Logger.Exception(ex);
        }
        Logger.Notice("Chainloader startup complete");
    }

    private IList<ModuleInfo> LoadModules(IList<ModuleInfo> modules)
    {
        var sortedModules = ModifyLoadOrder(modules);
        var invalidModules = new HashSet<string>();
        var processedModules = new Dictionary<string, SemanticVersioning.Version>();
        var loadedAssemblies = new Dictionary<string, Assembly>();
        var loadedModules = new List<ModuleInfo>();
        foreach (ModuleInfo module in sortedModules)
        {
            var dependsOnInvalidModule = false;
            var missingDependencies = new List<ArchiveDependency>();
            foreach (var dependency in module.Dependencies)
            {
                static bool IsHardDependency(ArchiveDependency dep) =>
                    (dep.Flags & ArchiveDependency.DependencyFlags.HardDependency) != 0;

                bool dependencyExists = processedModules.TryGetValue(dependency.DependencyGUID, out var moduleVersion);
                if (!dependencyExists)
                {
                    dependencyExists = Modules.TryGetValue(dependency.DependencyGUID, out var moduleInfo);
                    moduleVersion = moduleInfo != null ? moduleInfo.Metadata.Version : null;
                    if (!dependencyExists)
                    {
#if BepInEx
                        dependencyExists = BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.TryGetValue(dependency.DependencyGUID, out var pluginInfo);
                        moduleVersion = pluginInfo != null ? pluginInfo.Metadata.Version : null;
#endif
                    }
                }
                if (!dependencyExists || dependency.VersionRange != null && !dependency.VersionRange.IsSatisfied(moduleVersion, false))
                {
                    if (IsHardDependency(dependency))
                        missingDependencies.Add(dependency);
                    continue;
                }
                if (invalidModules.Contains(dependency.DependencyGUID) && IsHardDependency(dependency))
                {
                    dependsOnInvalidModule = true;
                    break;
                }
            }

            processedModules.Add(module.Metadata.GUID, module.Metadata.Version);

            if (dependsOnInvalidModule)
            {
                string message = $"Skipping [{module}] because it has a dependency that was not loaded. See previous errors for details.";
                DependencyErrors.Add(message);
                Logger.Warning(message);
            }
            else if (missingDependencies.Count != 0)
            {
                var message = $@"Could not load [{module}] because it has missing dependencies: {
                    string.Join(", ", missingDependencies.Select(s => s.VersionRange == null ? s.DependencyGUID : $"{s.DependencyGUID} ({s.VersionRange})").ToArray())
                    }";
                DependencyErrors.Add(message);
                Logger.Error(message);
                invalidModules.Add(module.Metadata.GUID);
                continue;
            }
            try
            {
                if (!loadedAssemblies.TryGetValue(module.Location, out var ass))
                {
                    ass = loadedAssemblies[module.Location] = Assembly.LoadFrom(module.Location);
                }
                Modules[module.Metadata.GUID] = module;
                TryRunModuleCtor(module, ass);
                module.Instance = LoadModule(module, ass);
                loadedModules.Add(module);
                ModuleLoaded?.Invoke(module);
            }
            catch (Exception ex)
            {
                invalidModules.Add(module.Metadata.GUID);
                Modules.Remove(module.Metadata.GUID);

                Logger.Error($"Error loading [{module}]:");
                Logger.Exception(ex);
            }
        }
        return loadedModules;
    }

    public IList<ModuleInfo> LoadModule(params string[] modulesPaths)
    {
        List<ModuleInfo> modules = new List<ModuleInfo>();
        foreach (string modulesPath in modulesPaths)
        {
            modules.AddRange(DiscoverModulesFrom(modulesPath, "TheArchive_ModuleChainloader"));
        }
        return LoadModules(modules);
    }

    private static void TryRunModuleCtor(ModuleInfo module, Assembly assembly)
    {
        try
        {
            RuntimeHelpers.RunModuleConstructor(assembly.GetType(module.TypeName).Module.ModuleHandle);
        }
        catch (Exception ex)
        {
            Logger.Error($"Couldn't run Module constructor for {assembly.FullName}::{module.TypeName}:");
            Logger.Exception(ex);
        }
    }

    public IArchiveModule LoadModule(ModuleInfo moduleInfo, Assembly moduleAssembly)
    {
        return ArchiveMod.CreateAndInitModule(moduleAssembly.GetType(moduleInfo.TypeName));
    }

    protected static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

    protected static readonly System.Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

    public static ArchiveModuleChainloader Instance { get; private set; }
}