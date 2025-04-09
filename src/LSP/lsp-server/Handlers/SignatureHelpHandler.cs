using System.Diagnostics;
using System.Text;
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
			var file = _solver.GetFile(request.TextDocument);
			if (file == null)
				return null;
			var project = _solver.Workspace.GetFileProject(file.Path);
			var reasoner = project.GetReasoner(file);

			var node = _solver.GetNodeAt(file, ConversionHelper.FromTo(request.Position));
			if (node == null)
				return null;

			// 1. If token is closed paren of an ArgsListNode, get args count to know index. `(|)` or `(..., |)`
			//	  - If there's no comma, index is last arg. If there's comma, index is last + 1.
			// 2. If any other token, find first ArgNode in ancestors and self.

			ArgsListNode? argsList = null;
			var index = -1;

			var pos = ConversionHelper.FromTo(request.Position);
			var tokenAtPos = reasoner.Tree.GetTokenAtWithTrivia(pos);
			if (tokenAtPos == null)
				return null;

			{
				if (tokenAtPos.IsSimpleTokenOfKind(SimpleTokenKind.ClosedParen, out _) && tokenAtPos.Parent is ArgsListNode n)
				{
					argsList = n;
					var lastArg = argsList.Args.LastOrDefault();
					if (lastArg == null)
					{
						index = 0;
					}
					else if (lastArg.CommaToken != null)
					{
						index = argsList.Args.Count();
					}
					else
					{
						index = argsList.Args.Count() - 1;
					}
				}
				else
				{
					var argNode = tokenAtPos.Parent.AncestorsAndSelf().OfType<ArgNode>().FirstOrDefault();
					if (argNode == null)
						return null;
					argsList = (ArgsListNode)argNode.Parent!;
					index = argsList.Args.ToList().IndexOf(argNode);
				}
			}

			Debug.Assert(argsList != null);
			Debug.Assert(index >= 0);

			// TODO: Support args list in AttrNode?

			if (argsList.Parent is not CallExprNode callExpr)
				return null;

			var (fn, fnType) = reasoner.LookupFnSymbolOrType(callExpr);
			if (fnType == null)
				return null;

			_logger.LogInformation("Index = {Index}", index);

			if (fn != null)
			{
				var b = new SigAndLabelRangeBuilder();
				if (!fn.IsMethod)
				{
					b.AddNormal("fn ");
				}
				b.AddNormal(fn.Name);
				b.AddNormal("(");
				for (var i = 0; i < fn.Params.Length; i++)
				{
					var last = i == fn.Params.Length - 1;
					var p = fn.Params[i];
					var paramLabel = $"{p.Name}: {p.Type.Name}{(last ? string.Empty : ", ")}";
					b.AddParam(paramLabel, documentation: string.Empty);
				}
				b.AddNormal(")");
				if (fn.ReturnType.KnownTypeTag is not KnownTypeTag.@void)
				{
					b.AddNormal(" ");
					b.AddNormal(fnType.ReturnType.Name);
				}

				return new SignatureHelp
				{
					ActiveParameter = index,
					//ActiveSignature =
					Signatures = new(new SignatureInformation
					{
						Label = b.SigLabel,
						//Documentation = new MarkupContent
						//{
						//	Kind = MarkupKind.Markdown,
						//	Value = ,
						//},
						Parameters = new(b.Params),
					}),
				};
			}
			else
			{
				var b = new SigAndLabelRangeBuilder();
				b.AddNormal("fn (");
				for (var i = 0; i < fnType.ParamTypes.Length; i++)
				{
					var last = i == fnType.ParamTypes.Length - 1;
					var p = fnType.ParamTypes[i];
					var paramLabel = $"{p.Name}{(last ? string.Empty : ", ")}";
					b.AddParam(paramLabel, documentation: string.Empty);
				}
				b.AddNormal(")");
				if(fnType.ReturnType.KnownTypeTag is not KnownTypeTag.@void)
				{
					b.AddNormal(" ");
					b.AddNormal(fnType.ReturnType.Name);
				}

				return new SignatureHelp
				{
					ActiveParameter = index,
					//ActiveSignature =
					Signatures = new(new SignatureInformation
					{
						Label = b.SigLabel,
						//Documentation =
						Parameters = new(b.Params),
					}),
				};
			}
		});
	}

	private class SigAndLabelRangeBuilder
	{
		private readonly StringBuilder _sb = new();
		private readonly List<ParameterInformation> _params = [];

		public string SigLabel => _sb.ToString();
		public List<ParameterInformation> Params => _params;

		public void AddNormal(string v)
		{
			_sb.Append(v);
		}

		public void AddParam(string label, string documentation)
		{
			var start = _sb.Length;
			var end = start + label.Length;
			_sb.Append(label);
			_params.Add(new ParameterInformation
			{
				Label = new ParameterInformationLabel((start, end)),
				Documentation = documentation,
			});
		}
	}

	protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			TriggerCharacters = new Container<string>("("),
			RetriggerCharacters = new Container<string>(","),
		};
	}
}
