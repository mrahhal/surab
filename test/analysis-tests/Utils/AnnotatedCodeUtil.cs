using System.Text;

namespace Surab.Analysis;

/// <summary>
/// Supports parsing code interspersed with special annotation syntaxto be used for various things.
/// </summary>
public static class AnnotatedCodeUtil
{
	/// <summary>
	/// Parses annotated code.
	///
	/// Supported syntax:
	/// - [|text|]
	/// - {|name:text|}
	/// </summary>
	/// <param name="annotatedCode">The annotated code.</param>
	/// <param name="treatAnnotatedContentAsCode">If true, the annotation content will be treated as part of the code.</param>
	public static AnnotatedCode Parse(string annotatedCode, bool treatAnnotatedContentAsCode)
	{
		var codeSB = new StringBuilder();
		var annotations = new List<Annotation>();

		var offset = 0;
		while (offset < annotatedCode.Length)
		{
			if (Check("[|"))
			{
				offset += 2;

				var innerAnnotationStart = offset;
				var diagStart = codeSB.Length;
				while (!Check("|]"))
				{
					if (offset >= annotatedCode.Length)
					{
						throw new Exception("Could not found closing annotation '|]'.");
					}

					offset++;
				}

				var slice = annotatedCode[innerAnnotationStart..offset];
				offset += 2;
				if (treatAnnotatedContentAsCode)
				{
					codeSB.Append(slice);
				}
				var diagEnd = codeSB.Length;
				annotations.Add(new Annotation(null, slice, new FileSpan(diagStart, diagEnd)));
			}

			if (Check("{|"))
			{
				offset += 2;

				var innerAnnotationStart = offset;
				var diagStart = codeSB.Length;
				while (!Check(":"))
				{
					if (offset >= annotatedCode.Length)
					{
						throw new Exception("Could not found annotation token ':'.");
					}

					offset++;
				}
				var name = annotatedCode[innerAnnotationStart..offset];
				offset += 1;

				innerAnnotationStart = offset;
				while (!Check("|}"))
				{
					if (offset >= annotatedCode.Length)
					{
						throw new Exception("Could not found closing annotation '|}'.");
					}

					offset++;
				}
				var slice = annotatedCode[innerAnnotationStart..offset];
				offset += 2;
				if (treatAnnotatedContentAsCode)
				{
					codeSB.Append(slice);
				}
				var diagEnd = codeSB.Length;
				annotations.Add(new Annotation(name, slice, new FileSpan(diagStart, diagEnd)));
			}

			if (offset < annotatedCode.Length)
			{
				// Might happen if the string ends with |} for example.
				codeSB.Append(annotatedCode[offset]);
			}
			offset++;
		}

		return new AnnotatedCode { Code = codeSB.ToString(), Annotations = annotations };

		bool Check(string part)
		{
			if (annotatedCode.AsSpan()[offset..].StartsWith(part))
			{
				return true;
			}
			return false;
		}
	}

	public record AnnotatedCode
	{
		public required string Code { get; init; }

		public required IReadOnlyList<Annotation> Annotations { get; init; }
	}

	public record Annotation(string? Name, string Content, FileSpan FileSpan);
}
