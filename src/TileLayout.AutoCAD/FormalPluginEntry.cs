using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(TileLayout.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(TileLayout.AutoCAD.TileLayoutCommands))]

namespace TileLayout.AutoCAD
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage(
                    "\nTileLayout 0.1.0 正式插件已加载。输入 TILE600 开始排版。");
            }
        }

        public void Terminate()
        {
        }
    }
}
