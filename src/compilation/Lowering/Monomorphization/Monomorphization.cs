namespace Surab.Compilation;

// In regards to generics, the following is description of some terminology used.
// Closed: For a generic struct S, S<X> is a closed generic type.
// Concrete: For a generic struct S, a closed generic that doesn't include any generic param is called concrete.

/// <summary>
/// Responsible for computing all generic concretes from a lowered project.
///
/// This process is known as monomorphization, i.e. converting a polymorphic symbol to many monomorphic symbols.
/// </summary>
internal static class Monomorphization
{
	public static IReadOnlyList<HirTree> Monomorph(HirLoweredProject project, LoweringInterner interner)
	{
		var collection = Collect(project, interner);
		return Monomorph(project, collection, interner);
	}

	public static MonomorphizationCollection Collect(HirLoweredProject project, LoweringInterner interner)
	{
		return MonomorphizationCollect.Collect(project, interner);
	}

	public static IReadOnlyList<HirTree> Monomorph(
		HirLoweredProject project,
		MonomorphizationCollection collection,
		LoweringInterner interner)
	{
		return MonomorphizationMonomorph.Monomorph(project, collection, interner);
	}
}
