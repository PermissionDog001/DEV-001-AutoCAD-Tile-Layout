using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TileLayout.AutoCAD.Adapter
{
    internal static class OrthogonalLayoutDimensionStyle
    {
        internal const string StyleName = "TILE_LAYOUT_ANNOTATION";
        private const string ArchitecturalTickBlockName = "_ARCHTICK";
        private const string FallbackArchitecturalTickBlockName =
            "TILE_LAYOUT_ARCHTICK";

        internal static DimStyleTableRecord CreateDefinition(
            ObjectId textStyleId,
            ObjectId architecturalTickBlockId)
        {
            var style = new DimStyleTableRecord
            {
                Name = StyleName,
                Dimasz = 80.0,
                Dimtxt = 100.0,
                Dimexe = 50.0,
                Dimexo = 20.0,
                Dimgap = 20.0,
                Dimclrd = ByBlockColor(),
                Dimclre = ByBlockColor(),
                Dimclrt = ByBlockColor(),
                Dimblk = architecturalTickBlockId,
                Dimblk1 = architecturalTickBlockId,
                Dimblk2 = architecturalTickBlockId,
                Dimldrblk = ObjectId.Null,
                Dimltex1 = ObjectId.Null,
                Dimltex2 = ObjectId.Null,
                Dimltype = ObjectId.Null,
                Dimtxsty = textStyleId,
                Dimdec = 0,
                Dimlfac = 1.0,
                Dimscale = 1.0,
                Dimlunit = 2,
                Dimlwd = LineWeight.ByLayer,
                Dimlwe = LineWeight.ByLayer,
                Dimtad = 1,
                Dimtih = false,
                Dimtoh = false,
                Dimtix = false,
                Dimtofl = true,
                Dimse1 = false,
                Dimse2 = false,
                Dimsah = false,
                Dimtsz = 0.0,
                Dimcen = 0.0,
                Dimalt = false,
                Dimtol = false,
                Dimrnd = 0.0,
                Dimpost = string.Empty,
                Dimapost = string.Empty
            };
            return style;
        }

        internal static ObjectId Ensure(
            Database database,
            Transaction transaction)
        {
            DimStyleTable table = (DimStyleTable)transaction.GetObject(
                database.DimStyleTableId,
                OpenMode.ForRead);
            ObjectId textStyleId = database.Textstyle;
            ObjectId architecturalTickBlockId =
                EnsureArchitecturalTickBlock(database, transaction);
            if (table.Has(StyleName))
            {
                ObjectId styleId = table[StyleName];
                DimStyleTableRecord existing =
                    (DimStyleTableRecord)transaction.GetObject(
                        styleId,
                        OpenMode.ForWrite);
                ApplyDefinition(
                    existing,
                    textStyleId,
                    architecturalTickBlockId);
                return styleId;
            }

            table.UpgradeOpen();
            DimStyleTableRecord style = CreateDefinition(
                textStyleId,
                architecturalTickBlockId);
            ObjectId createdStyleId = table.Add(style);
            transaction.AddNewlyCreatedDBObject(style, true);
            return createdStyleId;
        }

        internal static void ApplyTransient(
            Dimension dimension,
            ObjectId textStyleId,
            ObjectId architecturalTickBlockId)
        {
            using (DimStyleTableRecord style = CreateDefinition(
                textStyleId,
                architecturalTickBlockId))
            {
                dimension.SetDimstyleData(style);
            }
        }

        internal static void ApplyDefinition(
            DimStyleTableRecord target,
            ObjectId textStyleId,
            ObjectId architecturalTickBlockId)
        {
            using (DimStyleTableRecord definition = CreateDefinition(
                textStyleId,
                architecturalTickBlockId))
            {
                target.Dimasz = definition.Dimasz;
                target.Dimtxt = definition.Dimtxt;
                target.Dimexe = definition.Dimexe;
                target.Dimexo = definition.Dimexo;
                target.Dimgap = definition.Dimgap;
                target.Dimclrd = definition.Dimclrd;
                target.Dimclre = definition.Dimclre;
                target.Dimclrt = definition.Dimclrt;
                target.Dimblk = definition.Dimblk;
                target.Dimblk1 = definition.Dimblk1;
                target.Dimblk2 = definition.Dimblk2;
                target.Dimldrblk = definition.Dimldrblk;
                target.Dimltex1 = definition.Dimltex1;
                target.Dimltex2 = definition.Dimltex2;
                target.Dimltype = definition.Dimltype;
                target.Dimtxsty = definition.Dimtxsty;
                target.Dimdec = definition.Dimdec;
                target.Dimlfac = definition.Dimlfac;
                target.Dimscale = definition.Dimscale;
                target.Dimlunit = definition.Dimlunit;
                target.Dimlwd = definition.Dimlwd;
                target.Dimlwe = definition.Dimlwe;
                target.Dimtad = definition.Dimtad;
                target.Dimtih = definition.Dimtih;
                target.Dimtoh = definition.Dimtoh;
                target.Dimtix = definition.Dimtix;
                target.Dimtofl = definition.Dimtofl;
                target.Dimse1 = definition.Dimse1;
                target.Dimse2 = definition.Dimse2;
                target.Dimsah = definition.Dimsah;
                target.Dimtsz = definition.Dimtsz;
                target.Dimcen = definition.Dimcen;
                target.Dimalt = definition.Dimalt;
                target.Dimtol = definition.Dimtol;
                target.Dimrnd = definition.Dimrnd;
                target.Dimpost = definition.Dimpost;
                target.Dimapost = definition.Dimapost;
            }
        }

        internal static ObjectId GetTransientArchitecturalTickBlockId(
            Database database)
        {
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                return FindArchitecturalTickBlock(
                    database,
                    transaction);
            }
        }

        private static ObjectId EnsureArchitecturalTickBlock(
            Database database,
            Transaction transaction)
        {
            ObjectId existing = FindArchitecturalTickBlock(
                database,
                transaction);
            if (!existing.IsNull)
            {
                return existing;
            }

            BlockTable table = (BlockTable)transaction.GetObject(
                database.BlockTableId,
                OpenMode.ForRead);
            table.UpgradeOpen();
            var record = new BlockTableRecord
            {
                Name = FallbackArchitecturalTickBlockName
            };
            ObjectId blockId = table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            var tick = new Line(
                new Point3d(-0.5, -0.5, 0.0),
                new Point3d(0.5, 0.5, 0.0));
            record.AppendEntity(tick);
            transaction.AddNewlyCreatedDBObject(tick, true);
            return blockId;
        }

        private static ObjectId FindArchitecturalTickBlock(
            Database database,
            Transaction transaction)
        {
            BlockTable table = (BlockTable)transaction.GetObject(
                database.BlockTableId,
                OpenMode.ForRead);
            if (table.Has(ArchitecturalTickBlockName))
            {
                return table[ArchitecturalTickBlockName];
            }

            if (table.Has("ARCHTICK"))
            {
                return table["ARCHTICK"];
            }

            if (table.Has(FallbackArchitecturalTickBlockName))
            {
                return table[FallbackArchitecturalTickBlockName];
            }

            return ObjectId.Null;
        }

        private static Color ByBlockColor()
        {
            return Color.FromColorIndex(ColorMethod.ByBlock, 0);
        }
    }
}
