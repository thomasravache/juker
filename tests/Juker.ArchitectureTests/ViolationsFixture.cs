using Juker.ArchitectureTests.Rules;
using Juker.ArchitectureTests.Violations;

namespace Juker.ArchitectureTests;

/// <summary>O assembly de fixture do IT-02, avaliado como produção e como projeto de teste ao mesmo tempo.</summary>
internal static class ViolationsFixture
{
    public static ArchitectureSubject Subject()
    {
        var assembly = typeof(ViolationsAssemblyMarker).Assembly;
        return ArchitectureSubject.From([assembly], [assembly]);
    }
}
