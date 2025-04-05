using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class SignatureHelpHandler(
	ILogger<DeclarationHandler> logger,
	SurabSolver solver) : SignatureHelpHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			var sourceFile = _solver.GetFile(request.TextDocument);
			if (sourceFile == null)
				return null;

			var node = _solver.GetNodeAt(sourceFile, ConversionHelper.FromTo(request.Position));
			if (node == null)
				return null;

			CallExprNode callExpr;
			var argIndex = (int?)null;

			// Quick hack so that if caret is at start `(...|)` treat last arg as the active one.
			if (node is ArgsListNode argsList &&
				argsList.Parent is CallExprNode callExpr_)
			{
				callExpr = callExpr_;

				if (argsList.ClosedParenToken.Span.Line.Start == ConversionHelper.FromTo(request.Position))
				{
					var lastArgIndex = argsList.Args.Count() - 1;
					if (lastArgIndex >= 0)
					{
						argIndex = lastArgIndex;
					}
				}
				else
				{
					return null;
				}
			}
			// TODO: Should be scoped better otherwise this will find args list of outer fn calls.
			else if (node.AncestorsAndSelf().OfType<ArgNode>().FirstOrDefault() is ArgNode arg &&
				arg.Parent is ArgsListNode argsList2 &&
				argsList2.Parent is CallExprNode callExpr_2)
			{
				callExpr = callExpr_2;

				var args = argsList2.Args.ToList();
				argIndex = arg == null ? null : args.IndexOf(arg);
			}
			else
			{
				return null;
			}

			var project = _solver.Workspace.GetFileProject(sourceFile.Path);
			var reasoner = project.GetReasoner(sourceFile);
			var (fn, fnType) = reasoner.LookupFnSymbolOrType(callExpr);
			if (fnType == null)
				return null;

			if (fn != null)
			{
				IEnumerable<ParameterInformation> @params = fn.Params.Select(p => new ParameterInformation
				{
					Label = new(p.Name ?? string.Empty),
					//Documentation =
				});

				return new SignatureHelp
				{
					ActiveParameter = argIndex,
					//ActiveSignature =
					Signatures = new(new SignatureInformation
					{
						//ActiveParameter =
						//Documentation =
						Label = fn.ToString(),
						Parameters = new(@params),
					}),
				};
			}
			else
			{
				IEnumerable<ParameterInformation> @params = fnType.ParamTypes.Select(p => new ParameterInformation
				{
					Label = new(p.Name ?? string.Empty),
				});

				return new SignatureHelp
				{
					ActiveParameter = argIndex,
					Signatures = new(new SignatureInformation
					{
						Label = fnType.ToString(),
						Parameters = new(@params),
					}),
				};
			}
		});
	}

	protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}
}
