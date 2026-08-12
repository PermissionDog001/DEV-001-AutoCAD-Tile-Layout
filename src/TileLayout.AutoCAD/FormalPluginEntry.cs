using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(TileLayout.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(TileLayout.AutoCAD.TileLayoutCommands))]

namespace TileLayout.AutoCAD
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public const string CurrentVersion = "0.2.1";

        public void Initialize()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage(string.Format(
                    "\nTileLayout V{0} 正式插件已加载。输入 TILEUI 打开引导式排版；"
                        + "TILEORTHOUI 为兼容入口。",
                    CurrentVersion));
            }
        }

        public void Terminate()
        {
        }
    }
}
