using Hikaria.Core;
using TheArchive.Core;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Localization;
using TheArchive.Interfaces;

namespace Hikaria.DropItem
{
    [ArchiveDependency(CoreGlobal.GUID, ArchiveDependency.DependencyFlags.HardDependency)]
    [ArchiveModule(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class EntryPoint : IArchiveModule
    {
        public void Init()
        {
        }

        public string ModuleGroup => FeatureGroups.GetOrCreateModuleGroup(PluginInfo.GUID);
        public ILocalizationService LocalizationService { get; set; }
        public IArchiveLogger Logger { get; set; }
    }
}
