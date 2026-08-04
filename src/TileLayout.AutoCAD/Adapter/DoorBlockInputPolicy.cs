using System;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public static class DoorBlockInputPolicy
    {
        public static DoorObjectRecognitionResult Validate(
            bool isBlockReference,
            bool isTopLevelModelSpace,
            bool isDynamicBlock,
            bool isExternalReference,
            double scaleX,
            double scaleY,
            double scaleZ)
        {
            return ValidateCore(
                isBlockReference,
                isTopLevelModelSpace,
                isDynamicBlock,
                isExternalReference,
                scaleX,
                scaleY,
                scaleZ,
                false);
        }

        public static DoorObjectRecognitionResult
            ValidateForFrozenSignatureInspection(
                bool isBlockReference,
                bool isTopLevelModelSpace,
                bool isDynamicBlock,
                bool isExternalReference,
                double scaleX,
                double scaleY,
                double scaleZ)
        {
            return ValidateCore(
                isBlockReference,
                isTopLevelModelSpace,
                isDynamicBlock,
                isExternalReference,
                scaleX,
                scaleY,
                scaleZ,
                true);
        }

        private static DoorObjectRecognitionResult ValidateCore(
            bool isBlockReference,
            bool isTopLevelModelSpace,
            bool isDynamicBlock,
            bool isExternalReference,
            double scaleX,
            double scaleY,
            double scaleZ,
            bool allowStaticSignatureInspection)
        {
            if (!isBlockReference)
            {
                return DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    DoorObjectRecognitionRejectionCode.UnsupportedObjectType,
                    "对象模式只支持用户显式选择一个模型空间顶层块参照；散 LINE/ARC 和其他对象不支持。");
            }

            if (!isTopLevelModelSpace)
            {
                return DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    DoorObjectRecognitionRejectionCode.NotTopLevelModelSpace,
                    "所选对象不是当前模型空间的顶层块参照。");
            }

            if (isExternalReference)
            {
                return DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    DoorObjectRecognitionRejectionCode.ExternalReference,
                    "外部参照中的门对象首轮不支持。");
            }

            if (!isDynamicBlock && !allowStaticSignatureInspection)
            {
                return DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    DoorObjectRecognitionRejectionCode.StaticBlock,
                    "所选块不是动态块；该调用路径没有授权静态几何签名检查。");
            }

            if (!HasUniformNonZeroScale(scaleX, scaleY, scaleZ))
            {
                return DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    DoorObjectRecognitionRejectionCode.NonUniformScaling,
                    "所选块或其嵌套使用非均匀缩放，圆弧门几何不支持。");
            }

            return null;
        }

        public static bool HasUniformNonZeroScale(
            double scaleX,
            double scaleY,
            double scaleZ)
        {
            double x = Math.Abs(scaleX);
            double y = Math.Abs(scaleY);
            double z = Math.Abs(scaleZ);
            return IsFinite(x)
                && IsFinite(y)
                && IsFinite(z)
                && x > GeometryTolerance.Coordinate
                && Math.Abs(x - y) <= GeometryTolerance.Coordinate
                && Math.Abs(x - z) <= GeometryTolerance.Coordinate;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
