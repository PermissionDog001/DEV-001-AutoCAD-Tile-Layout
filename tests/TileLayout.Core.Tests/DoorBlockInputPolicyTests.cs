using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorBlockInputPolicyTests
    {
        [TestMethod]
        public void Validate_TopLevelDynamicUniformBlock_IsSupported()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                true,
                true,
                true,
                false,
                -2.0,
                2.0,
                2.0);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Validate_DynamicOnlyRoute_StillRejectsStaticBlock()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                true,
                true,
                false,
                false,
                1.0,
                1.0,
                1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.StaticBlock,
                result.RejectionCode);
        }

        [TestMethod]
        public void ValidateForFrozenSignatureInspection_StaticUniformBlock_IsReadableButNotAccepted()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy
                .ValidateForFrozenSignatureInspection(
                    true,
                    true,
                    false,
                    false,
                    -1000.0,
                    -1000.0,
                    1000.0);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ValidateForFrozenSignatureInspection_StaticNonUniformBlock_IsRejected()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy
                .ValidateForFrozenSignatureInspection(
                    true,
                    true,
                    false,
                    false,
                    1200.0,
                    -1200.0,
                    300.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.NonUniformScaling,
                result.RejectionCode);
        }

        [TestMethod]
        public void ValidateForFrozenSignatureInspection_KeepsTopLevelAndXrefGates()
        {
            DoorObjectRecognitionResult nested = DoorBlockInputPolicy
                .ValidateForFrozenSignatureInspection(
                    true,
                    false,
                    false,
                    false,
                    1.0,
                    1.0,
                    1.0);
            DoorObjectRecognitionResult xref = DoorBlockInputPolicy
                .ValidateForFrozenSignatureInspection(
                    true,
                    true,
                    false,
                    true,
                    1.0,
                    1.0,
                    1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.NotTopLevelModelSpace,
                nested.RejectionCode);
            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.ExternalReference,
                xref.RejectionCode);
        }

        [TestMethod]
        public void Validate_NonUniformScale_IsUnsupported()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                true,
                true,
                true,
                false,
                1.0,
                2.0,
                1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.NonUniformScaling,
                result.RejectionCode);
            Assert.AreEqual(DoorObjectRecognitionStatus.Unsupported, result.Status);
        }

        [TestMethod]
        public void Validate_ExternalReference_IsUnsupportedBeforeGeometry()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                true,
                true,
                true,
                true,
                1.0,
                1.0,
                1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.ExternalReference,
                result.RejectionCode);
        }

        [TestMethod]
        public void Validate_ScatteredLineOrArc_IsUnsupportedObjectType()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                false,
                true,
                false,
                false,
                1.0,
                1.0,
                1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.UnsupportedObjectType,
                result.RejectionCode);
        }

        [TestMethod]
        public void Validate_NestedOrPaperSpaceBlock_IsUnsupported()
        {
            DoorObjectRecognitionResult result = DoorBlockInputPolicy.Validate(
                true,
                false,
                true,
                false,
                1.0,
                1.0,
                1.0);

            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.NotTopLevelModelSpace,
                result.RejectionCode);
        }
    }
}
