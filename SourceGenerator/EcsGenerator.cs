using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator {
    [Generator]
    public class EcsGenerator : ISourceGenerator {
        public const string ECS_LAMBDA_KEY = "//ecsLambda";
        public void Initialize(GeneratorInitializationContext context) {
            context.RegisterForSyntaxNotifications(() => new MainSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            var receiver = context.SyntaxReceiver as MainSyntaxReceiver;

            for (var i = 0; i < receiver.Reader.InfoList.Count; i++) {
                var info = receiver.Reader.InfoList[i];
                
                var usings = GenerateUsingSource(info.Usings);
                var eachLambdas = info.EachLambdas;
                var pools = string.Empty;
                //Pools fields
                var poolsInit = string.Empty;
                pools = GeneratePoolsSource(info, pools, ref poolsInit);

                var queries = string.Empty;
                var initQueries = string.Empty;
                //Queries fields
                queries = GenerateQueiresSource(eachLambdas, info, queries, ref initQueries);

                var lambdas = string.Empty;
                //Generate for loop
                lambdas = GenerateLoopsSource(eachLambdas, info);

                //var fullMethod = GenerateMethodSource(info.MethodBody, lambdas);
                
                var namespaceName = info.NamespaceName != null ? $"namespace {info.NamespaceName}" : "";
                
                var newSystem = @"
// Generated system source

" + usings + @"
" + namespaceName + $@"
{(namespaceName != string.Empty ? "{" : string.Empty)}" + @"
    public partial class " + info.SystemType + @"
    {
    " + pools + @"
    " + queries + @"

        public override void OnInit()
        {
            " + poolsInit + @"
            " + initQueries + @"
        }
        public void Update_Generated()" + @"
        {
            //CODE
            " + lambdas + @"
        }" + @"
        
        public override void UpdateN() {
            Update_Generated();
        }
    }
" + $"{(namespaceName != string.Empty ? "}" : string.Empty)}";
                context.AddSource(info.SystemType, SourceText.From(newSystem, Encoding.UTF8));
            }
        }

        private static string GenerateMethodSource(string method, string rootLambda) {
            var builder = new StringBuilder();
            builder.Append(method);
            builder.Replace(EcsGenerator.ECS_LAMBDA_KEY, rootLambda);
            builder.AppendLine();
            return builder.ToString();
        }
        private static string GenerateUsingSource(IEnumerable<string> usings) {
            var builder = new StringBuilder();
            foreach (var @using in usings) {
                builder.Append(@using);
            }
            return builder.ToString();
        }
        private static string GeneratePoolsSource(ReadSysntaxReceiver.Information info, string pools,
            ref string poolsInit) {
            foreach (var infoPoolsType in info.PoolsTypes) {
                pools += Environment.NewLine;
                pools += $"        private Pool<{infoPoolsType}> pool{infoPoolsType};";
                poolsInit += Environment.NewLine;
                poolsInit += $"             pool{infoPoolsType} = world.GetPool<{infoPoolsType}>();";
            }

            return pools;
        }


        private string GenerateLoopsSource(IReadOnlyList<ReadSysntaxReceiver.EachLamdaInfo> eachLambdas,
            ReadSysntaxReceiver.Information info) {
            var source = string.Empty;
            for (var i1 = 0; i1 < eachLambdas.Count; i1++) {
                var lambda = eachLambdas[i1];
                source += GenerateLambda(lambda, lambda.newQueryName, info);
            }

            return source;
        }

        private string GenerateLambda(ReadSysntaxReceiver.EachLamdaInfo info, string queryName,
            ReadSysntaxReceiver.Information information, int nestingDepth = 0) {
            var tab = "         ";
            for (var i = 0; i < nestingDepth + 1; i++) tab += " ";
            var builder = new StringBuilder();
            builder.AppendLine();
            builder.Append(
                $"{tab}  for(int index{info.depth} = 0; index{info.depth} < {queryName}.Count; index{info.depth}++)");
            builder.AppendLine();
            builder.Append($"{tab}" + "  {");
            builder.AppendLine();
            foreach (var t in info.Parameters) {
                builder.Append(
                    $"{tab}     ref var {t.Name} = ref pool{t.Type}.items[{queryName}.entities[index{info.depth}]];");
                builder.AppendLine();
            }

            builder.AppendLine();
            builder.Append($"{info.Body}");
            var (item1, depth, id) = HasNestedLamda(info.Body);
            if (item1) {
                var replacing = $"//LAMBDA_DEPTH:{depth},ID:{id}";
                //Log.Debug($"lambda_{depth}_{id}", replacing);
                if (information.LamdasMap.TryGetValue((depth, id), out var nestedInfo)) {
                    var temp = nestedInfo;
                    information.LamdasMap.Remove((depth, id));
                    information.EachLambdas.Remove(temp);

                    builder.Replace(replacing, GenerateLambda(temp, temp.newQueryName, information, depth));
                }
            }


            builder.AppendLine();
            builder.Append($"{tab}" + "  }");
            return builder.ToString();
        }
        private static string GenerateQueiresSource(IReadOnlyList<ReadSysntaxReceiver.EachLamdaInfo> eachLambdas,
            ReadSysntaxReceiver.Information info, string queries, ref string initQueries) {
            for (var i1 = 0; i1 < eachLambdas.Count; i1++) {
                var lambda = eachLambdas[i1];
                if (!info.QueryNames.Contains(lambda.newQueryName)) continue;
                queries += Environment.NewLine;
                queries += @"        private EntityQuery<With<";
                for (var i2 = 0; i2 < lambda.Parameters.Count; i2++) {
                    queries += lambda.Parameters[i2].Type;
                    if (i2 == lambda.Parameters.Count - 1)
                        queries += $">> {lambda.newQueryName};";
                    else
                        queries += ",";
                }

                initQueries += Environment.NewLine;
                initQueries += $"             {lambda.newQueryName} = new EntityQuery<With<";
                for (var i2 = 0; i2 < lambda.Parameters.Count; i2++) {
                    initQueries += lambda.Parameters[i2].Type;
                    if (i2 == lambda.Parameters.Count - 1)
                        initQueries += ">>(world)";
                    else
                        initQueries += ",";
                }

                if (lambda.WithoutParameters.Count > 0)
                    for (var i2 = 0; i2 < lambda.WithoutParameters.Count; i2++)
                        initQueries += $".Without<{lambda.WithoutParameters[i2].Type}>()";

                initQueries += ";";
                info.QueryNames.Remove(lambda.newQueryName);
            }

            return queries;
        }


        private (bool, int, int) HasNestedLamda(string body, string key = "LAMBDA_DEPTH") {
            if (!body.Contains(key)) return (false, 0, 0);
            var index = body.IndexOf("LAMBDA_DEPTH:", StringComparison.Ordinal);
            var depth = int.Parse($"{body[index + 13]}");
            var id = int.Parse($"{body[index + 18]}");
            return (true, depth, id);
        }
    }

    public class MainSyntaxReceiver : ISyntaxReceiver {
        public ReadSysntaxReceiver Reader { get; } = new ReadSysntaxReceiver();
        public WriteSyntaxReceiver Writer { get; } = new WriteSyntaxReceiver();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            Reader.OnVisitSyntaxNode(syntaxNode);
            Writer.OnVisitSyntaxNode(syntaxNode);
        }
    }

    public class ReadSysntaxReceiver : ISyntaxReceiver {
        public List<Information> InfoList = new List<Information>();
        private int lastId;

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            if (!(syntaxNode is ClassDeclarationSyntax node)) return;
            var IUpdateInterface = node.BaseList?.Types.FirstOrDefault();
            if (IUpdateInterface == null) return;

            var name = IUpdateInterface.Type.ToString();
            if (name != "UpdateSystem") return;
            if (!(node.Members.FirstOrDefault(x => x is MethodDeclarationSyntax) is MethodDeclarationSyntax method)
            ) return;
            FindEachMethod(method, node);
        }

        private void FindEachMethod(MethodDeclarationSyntax method, ClassDeclarationSyntax classNode) {
            if (method.Identifier.Text != "Update") return;
            var methodChilds = method.Body?.ChildNodes();
            if (methodChilds == null) return;
            var information = new Information();
            information.MethodBody = string.Empty;
            switch (classNode.Parent) {
                case NamespaceDeclarationSyntax namespaceDeclarationSyntax: {
                    if (namespaceDeclarationSyntax.Parent != null) {
                        information.Usings = namespaceDeclarationSyntax.Parent.ChildNodes()
                            .Where(x => x is UsingDirectiveSyntax)
                            .Select(x => x.GetText().ToString()).ToList();
                    }
                
                    information.NamespaceName = namespaceDeclarationSyntax.ChildNodes()
                        .FirstOrDefault(x => x is IdentifierNameSyntax)
                        ?.ToString();
                    break;
                }
                case CompilationUnitSyntax unitSyntax:
                    information.Usings = unitSyntax.ChildNodes()
                        .Where(x => x is UsingDirectiveSyntax)
                        .Select(x => x.GetText().ToString())
                        .ToList();
                    break;
            }
            information.Method = method;
            information.SystemType = classNode.Identifier.ToString();
            var body = string.Empty;
            foreach (var childNode in methodChilds) {
                if (childNode is ExpressionStatementSyntax expressionStatementSyntax) {
                    if (!IsEach(expressionStatementSyntax)) {
                        information.MethodBody += expressionStatementSyntax.GetText().ToString();
                        information.MethodBody += Environment.NewLine;
                    }
                    else {
                        information.MethodBody += EcsGenerator.ECS_LAMBDA_KEY;
                        information.MethodBody += Environment.NewLine;
                        var lambdaExpressionSyntax = GetLambdaExpression(expressionStatementSyntax);
                        if (lambdaExpressionSyntax == null) continue;

                        var eachInfo = new EachLamdaInfo {
                            WithoutParameters = GetWithoutParameters(expressionStatementSyntax), depth = 0, id = lastId
                        };

                        foreach (var syntaxNode in lambdaExpressionSyntax.Body.ChildNodes()) {
                            switch (syntaxNode) {
                                case ExpressionStatementSyntax expressionSyntax: {
                                    var expresion = expressionSyntax.Expression.ToString();
                                    if (expresion.Contains("entities"))
                                        expresion = $"//LAMBDA_DEPTH:{eachInfo.depth + 1},ID:{eachInfo.id}";
                                    body += $"                      {expresion};";
                                    body += Environment.NewLine;
                                    break;
                                }
                                case LocalDeclarationStatementSyntax localDeclarationStatementSyntax:
                                    body += localDeclarationStatementSyntax.GetText().ToString();
                                    body += Environment.NewLine;
                                    break;
                            }
                        }

                        eachInfo.Body = body;

                        for (var index = 0; index < lambdaExpressionSyntax.ParameterList.Parameters.Count; index++) {
                            var parameter = lambdaExpressionSyntax.ParameterList.Parameters[index];
                            if (parameter.Type == null) continue;
                            var paramType = parameter.Type.ToString();
                            if (!information.PoolsTypes.Contains(paramType))
                                information.PoolsTypes.Add(paramType);
                            eachInfo.Parameters.Add(new Parameter
                                {Type = paramType, Name = parameter.Identifier.ToString()});
                        }

                        eachInfo.newQueryName = "_query_";

                        for (var index = 0; index < eachInfo.Parameters.Count; index++) {
                            var infoParameter = eachInfo.Parameters[index];
                            eachInfo.newQueryName += $"{infoParameter.Type}";
                            if (index < eachInfo.Parameters.Count - 1)
                                eachInfo.newQueryName += "_";
                        }

                        if (!information.QueryNames.Contains(eachInfo.newQueryName))
                            information.QueryNames.Add(eachInfo.newQueryName);

                        if (!information.LamdasMap.ContainsKey((eachInfo.depth, eachInfo.id)))
                            information.LamdasMap.Add((eachInfo.depth, eachInfo.id), eachInfo);

                        information.EachLambdas.Add(eachInfo);

                        ReadLambda(lambdaExpressionSyntax, method, classNode, 6, 1, information);

                        lastId++;
                    }
                }
                else if (childNode is LocalDeclarationStatementSyntax localDeclarationStatementSyntax) {
                    information.MethodBody += localDeclarationStatementSyntax.GetText().ToString();
                    information.MethodBody += Environment.NewLine;
                }
            }

            InfoList.Add(information);
        }

        private void ReadLambda(
            ParenthesizedLambdaExpressionSyntax lambdaNode,
            MethodDeclarationSyntax methodNode,
            ClassDeclarationSyntax classNode,
            int maxDepth,
            int depth,
            Information information) {
            var lambdaChilds1 = lambdaNode.Body.ChildNodes();
            foreach (var lambdaChild in lambdaChilds1) {
                if (!(lambdaChild is ExpressionStatementSyntax expression)) continue;
                if (IsEach(expression)) {
                    var lambdaExpressionSyntax = GetLambdaExpression(expression);
                    var eachInfo = new EachLamdaInfo();
                    eachInfo.WithoutParameters = GetWithoutParameters(expression);
                    eachInfo.depth = depth;
                    eachInfo.id = lastId;
                    var body = string.Empty;
                    foreach (var syntaxNode in lambdaExpressionSyntax.Body.ChildNodes()) {
                        switch (syntaxNode) {
                            case ExpressionStatementSyntax expressionSyntax: {
                                var expresion = expressionSyntax.Expression.ToString();
                                if (expresion.Contains("entities"))
                                    expresion = $"//LAMBDA_DEPTH:{eachInfo.depth + 1},ID:{eachInfo.id}";
                                body += $"                      {expresion};";
                                body += Environment.NewLine;
                                break;
                            }
                            case LocalDeclarationStatementSyntax localDeclarationStatementSyntax:
                                body += localDeclarationStatementSyntax.GetText().ToString();
                                body += Environment.NewLine;
                                break;
                        }
                    }
                    eachInfo.Body = body;
                    for (var index = 0; index < lambdaExpressionSyntax.ParameterList.Parameters.Count; index++) {
                        var parameter = lambdaExpressionSyntax.ParameterList.Parameters[index];
                        if (parameter.Type != null) {
                            var paramType = parameter.Type.ToString();
                            if (!information.PoolsTypes.Contains(paramType))
                                information.PoolsTypes.Add(paramType);
                            eachInfo.Parameters
                                .Add(new Parameter
                                    {Type = paramType, Name = parameter.Identifier.ToString()});
                        }
                    }

                    eachInfo.newQueryName = "_query_";
                    //var paramString2 = string.Empty;
                    for (var index = 0; index < eachInfo.Parameters.Count; index++) {
                        var infoParameter = eachInfo.Parameters[index];
                        //paramString2 += "Name:" + infoParameter.Name + ", Type:" + infoParameter.Type +
                        //                Environment.NewLine;

                        eachInfo.newQueryName += $"{infoParameter.Type}";
                        if (index < eachInfo.Parameters.Count - 1)
                            eachInfo.newQueryName += "_";
                    }

                    if (!information.QueryNames.Contains(eachInfo.newQueryName))
                        information.QueryNames.Add(eachInfo.newQueryName);
                    //paramString2 += $"Deapth = {depth},";
                    //paramString2 += $"ID = {lastId},";

                    //paramString2 += $"Body = {eachInfo.Body}";
                    //Log.Debug($"Each Nested {depth}_{eachInfo.id}",paramString2);
                    information.EachLambdas.Add(eachInfo);
                    if (!information.LamdasMap.ContainsKey((eachInfo.depth, eachInfo.id)))
                        information.LamdasMap.Add((eachInfo.depth, eachInfo.id), eachInfo);
                    depth++;
                    if (maxDepth == depth) return;
                    ReadLambda(lambdaExpressionSyntax, methodNode, classNode, maxDepth, depth, information);
                }
            }
        }

        private bool IsEach(SyntaxNode node) {
            foreach (var syntaxNode in node.ChildNodes()) {
                if (!(syntaxNode is InvocationExpressionSyntax invocationExpressionSyntax)) continue;
                foreach (var childNode in invocationExpressionSyntax.ChildNodes())
                    if (childNode is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                        return memberAccessExpressionSyntax.Name.ToString() == "Each";
            }

            return false;
        }

        private List<WithoutParameter> GetWithoutParameters(SyntaxNode node) {
            var list = new List<WithoutParameter>();
            foreach (var syntaxNode in node.ChildNodes()) {
                if (!(syntaxNode is InvocationExpressionSyntax invocationExpressionSyntax)) continue;
                foreach (var childNode in invocationExpressionSyntax.ChildNodes()) {
                    if (!(childNode is MemberAccessExpressionSyntax memberAccessExpressionSyntax)) continue;
                    foreach (var syntaxNode1 in memberAccessExpressionSyntax.ChildNodes()) {
                        if (!(syntaxNode1 is InvocationExpressionSyntax expressionSyntax)) continue;
                        foreach (var childNode1 in expressionSyntax.ChildNodes()) {
                            if (!(childNode1 is MemberAccessExpressionSyntax memberAccessExpressionSyntax1)) continue;
                            foreach (var node1 in memberAccessExpressionSyntax1.ChildNodes()) {
                                if (!(node1 is GenericNameSyntax genericNameSyntax)) continue;
                                foreach (var typeSyntax in genericNameSyntax.TypeArgumentList.Arguments)
                                    list.Add(new WithoutParameter {Type = typeSyntax.GetText().ToString()});
                            }
                        }
                    }
                }
            }

            return list;
        }

        private static ParenthesizedLambdaExpressionSyntax GetLambdaExpression(SyntaxNode node) {
            foreach (var syntaxNode in node.ChildNodes()) {
                if (!(syntaxNode is InvocationExpressionSyntax invocationExpressionSyntax)) continue;
                foreach (var childNode1 in invocationExpressionSyntax.ChildNodes()) {
                    if (!(childNode1 is ArgumentListSyntax argumentListSyntax)) continue;
                    foreach (var node1 in argumentListSyntax.ChildNodes()) {
                        if (!(node1 is ArgumentSyntax argumentSyntax)) continue;
                        foreach (var syntaxNode2 in argumentSyntax.ChildNodes())
                            if (syntaxNode2 is ParenthesizedLambdaExpressionSyntax lambdaExpressionSyntax)
                                return lambdaExpressionSyntax;
                    }
                }
            }

            return null;
        }

        public class Information {
            public List<string> Usings = new List<string>();
            public readonly List<EachLamdaInfo> EachLambdas = new List<EachLamdaInfo>();
            public readonly Dictionary<(int, int), EachLamdaInfo> LamdasMap = new Dictionary<(int, int), EachLamdaInfo>();
            public readonly HashSet<string> PoolsTypes = new HashSet<string>();
            public readonly HashSet<string> QueryNames = new HashSet<string>();
            public MethodDeclarationSyntax Method;
            public string MethodBody;
            public string NamespaceName;
            public string SystemType;
        }
        
        public class Parameter {
            public string Name;
            public string Type;
        }

        public class WithoutParameter {
            public string Type;
        }

        public class EachLamdaInfo {
            public string Body;
            public int depth;
            public int id;
            public string newQueryName;
            public List<Parameter> Parameters = new List<Parameter>();
            public List<WithoutParameter> WithoutParameters = new List<WithoutParameter>();
        }
    }

    public class WriteSyntaxReceiver : ISyntaxReceiver {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) { }
    }

    public static class Log {
        public static void Debug(string fileName, string text) {
            var path = $@"D:\Unity\SourceGenerator\SourceGenerator\{fileName}.txt";
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllText(path, text);
        }
    }
}