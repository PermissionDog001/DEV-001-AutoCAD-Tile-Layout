using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(TileLayout.AutoCAD.Probe.PluginEntry))]
[assembly: CommandClass(typeof(TileLayout.AutoCAD.Probe.TileProbeCommands))]

namespace TileLayout.AutoCAD.Probe
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage(
                    "\nTileLayout 0.0.1 技术探针已加载。输入 TILE600PROBE 开始验证。");
            }
        }

        public void Terminate()
        {
        }
    }
}
