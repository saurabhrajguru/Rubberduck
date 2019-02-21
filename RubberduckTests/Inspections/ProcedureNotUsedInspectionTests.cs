using System;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using Rubberduck.Inspections.Concrete;
using Rubberduck.Parsing.Symbols;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;
using Rubberduck.VBEditor.SafeComWrappers.VBA;
using RubberduckTests.Mocks;

namespace RubberduckTests.Inspections
{
    [TestFixture]
    public class ProcedureNotUsedInspectionTests
    {
        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_ReturnsResult()
        {
            const string inputCode =
                @"Private Sub Foo()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(1, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_ReturnsResult_MultipleSubs()
        {
            const string inputCode =
                @"Private Sub Foo()
End Sub

Private Sub Goo()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(2, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureUsed_DoesNotReturnResult()
        {
            const string inputCode =
                @"Private Sub Foo()
    Goo
End Sub

Private Sub Goo()
    Foo
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_ReturnsResult_SomeProceduresUsed()
        {
            const string inputCode =
                @"Private Sub Foo()
End Sub

Private Sub Goo()
    Foo
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(1, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_DoesNotReturnResult_InterfaceImplementation()
        {
            //Input
            const string inputCode1 =
                @"Public Sub DoSomething(ByVal a As Integer)
End Sub";
            const string inputCode2 =
                @"Implements IClass1

Private Sub IClass1_DoSomething(ByVal a As Integer)
End Sub";

            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("TestProject1", ProjectProtection.Unprotected)
                .AddComponent("IClass1", ComponentType.ClassModule, inputCode1)
                .AddComponent("Class1", ComponentType.ClassModule, inputCode2)
                .Build();
            var vbe = builder.AddProject(project).Build();

            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_HandlerIsIgnoredForUnraisedEvent()
        {
            //Input
            const string inputCode1 = @"Public Event Foo(ByVal arg1 As Integer, ByVal arg2 As String)";
            const string inputCode2 =
                @"Private WithEvents abc As Class1

Private Sub abc_Foo(ByVal arg1 As Integer, ByVal arg2 As String)
End Sub";

            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("TestProject1", ProjectProtection.Unprotected)
                .AddComponent("Class1", ComponentType.ClassModule, inputCode1)
                .AddComponent("Class2", ComponentType.ClassModule, inputCode2)
                .Build();
            var vbe = builder.AddProject(project).Build();

            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count(result => result.Target.DeclarationType == DeclarationType.Procedure));
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_NoResultForClassInitialize()
        {
            //Input
            const string inputCode =
                @"Private Sub Class_Initialize()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleModule(inputCode, ComponentType.ClassModule, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_NoResultForCasedClassInitialize()
        {
            //Input
            const string inputCode =
                @"Private Sub class_initialize()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleModule(inputCode, ComponentType.ClassModule, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_NoResultForClassTerminate()
        {
            //Input
            const string inputCode =
                @"Private Sub Class_Terminate()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleModule(inputCode, ComponentType.ClassModule, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_NoResultForCasedClassTerminate()
        {
            //Input
            const string inputCode =
                @"Private Sub class_terminate()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleModule(inputCode, ComponentType.ClassModule, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [TestCase(ComponentType.StandardModule, "auto_open", "module1", "Excel")]
        [TestCase(ComponentType.StandardModule, "auto_close", "module1", "Excel")]
        [TestCase(ComponentType.StandardModule, "AutoExec", "module1", "Word")]
        [TestCase(ComponentType.StandardModule, "AutoNew", "module1", "Word")]
        [TestCase(ComponentType.StandardModule, "AutoOpen", "module1", "Word")]
        [TestCase(ComponentType.StandardModule, "AutoClose", "module1", "Word")]
        [TestCase(ComponentType.StandardModule, "AutoExit", "module1", "Word")]
        [TestCase(ComponentType.Document, "AutoExec", "module1", "Word")]
        [TestCase(ComponentType.Document, "AutoNew", "module1", "Word")]
        [TestCase(ComponentType.StandardModule, "Main", "AutoClose", "Word")]
        [TestCase(ComponentType.StandardModule, "Main", "AutoExit", "Word")]
        [Category("Inspections")]
        public void ProcedureNotUsed_NoResultForHostSpecificAutoMacros(ComponentType componentType, string macroName, string moduleName, string hostName)
        {
            //Input
            var inputCode =
                $@"Private Sub {macroName}()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleModule(inputCode, moduleName, componentType, out _);
            vbe.Setup(v => v.HostApplication().AutoMacroIdentifiers).Returns(new []
            {
                new HostAutoMacro(new[] {componentType}, true, moduleName, macroName)
            });
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ProcedureNotUsed_Ignored_DoesNotReturnResult()
        {
            const string inputCode =
                @"'@Ignore ProcedureNotUsed
Private Sub Foo()
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ProcedureNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.IsFalse(inspectionResults.Any());
            }
        }

        [Test]
        [Category("Inspections")]
        public void InspectionName()
        {
            const string inspectionName = "ProcedureNotUsedInspection";
            var inspection = new ProcedureNotUsedInspection(null);

            Assert.AreEqual(inspectionName, inspection.Name);
        }
    }
}
