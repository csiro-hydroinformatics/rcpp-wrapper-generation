﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Rcpp.CodeGen
{

    public interface IApiConverter
    {
        string ConvertLine(string line);
        string GetPreamble();
    }

    public class WrapperGenerator
    {
        public WrapperGenerator(IApiConverter converter)
        {
            this.converter = converter;
            this.filter = new HeaderFilter();
        }
        public WrapperGenerator(IApiConverter converter, HeaderFilter filter)
        {
            this.converter = converter;
            this.filter = filter;
        }

        IApiConverter converter;
        HeaderFilter filter;

        public void CreateWrapperHeader(string inputFile, string outputFile)
        {
            string[] lines = filter.Filter(inputFile);
            StringBuilder sb = new StringBuilder();
            sb.Append(converter.GetPreamble());
            string[] outputlines = Convert(lines);
            for (int i = 0; i < outputlines.Length; i++)
            {
                sb.Append(outputlines[i]);
            }
            string output = sb.ToString();
            File.WriteAllText(outputFile, output);
        }

        public string[] Convert(string[] lines)
        {
            //SWIFT_API ModelRunner * CloneModel(ModelRunner * src);
            //SWIFT_API ModelRunner * CreateNewFromNetworkInfo(NodeInfo * nodes, int numNodes, LinkInfo * links, int numLinks);
            List<string> converted = new List<string>();

            //sb.Append(PrependOutputFile);
            foreach (string lineRaw in lines)
            {
                string line = lineRaw.Trim();
                string convertedLine = converter.ConvertLine(line);
                converted.Add(convertedLine);
            }
            return converted.ToArray();
        }
    }

    public class HeaderFilter
    {
        public HeaderFilter()
        {
            ContainsAny = new string[] { "SWIFT_API" };
            ToRemove = new string[] { "SWIFT_API" };
            ContainsNone = new string[] { "#define" };
            NotStartsWith = new string[] { "//" };
        }

        public string[] Filter(string inputFile)
        {
            string input = File.ReadAllText(inputFile);
            return FindMatchingLines(input);
        }

        public string[] FindMatchingLines(string input)
        {
            //SWIFT_API ModelRunner * CloneModel(ModelRunner * src);
            //SWIFT_API ModelRunner * CreateNewFromNetworkInfo(NodeInfo * nodes, int numNodes, LinkInfo * links, int numLinks);
            List<string> output = new List<string>();
            using (var tr = new StringReader(input))
            {
                string line = "";
                while (line != null)
                {
                    line = line.Trim();
                    if (IsMatch(line))
                    {
                        line = prepareInLine(line);
                        output.Add(line);
                    }
                    line = tr.ReadLine();
                }
            }
            return output.ToArray();
        }

        private string prepareInLine(string line)
        {
            string s = line.Replace("\t", " ");
            s = s.Trim();
            s = removeToRemove(s);
            s = s.Trim();
            s = preprocessPointers(s);
            return s;
        }

        private static string preprocessPointers(string s)
        {
            // Make all pointers types without blanks
            var rexpPtr = new Regex(" *\\*");
            s = rexpPtr.Replace(s, "*");
            return s;
        }

        private string removeToRemove(string s)
        {
            foreach (var r in ToRemove)
                s = s.Replace(r, "");
            return s;
        }

        public bool IsMatch(string line)
        {
            line = line.Trim();
            if (StartsWithExcluded(line)) return false;
            bool match = false;
            if (ContainsAny.Length > 0)
            {
                foreach (string p in ContainsAny)
                    match = match || line.Contains(p);
                if (!match) return false;
            }
            match = true;
            foreach (string p in ContainsNone)
                if (line.Contains(p)) return false;

            return match;
        }

        private bool StartsWithExcluded(string line)
        {
            foreach (string p in NotStartsWith)
                if (line.StartsWith(p)) return true;
            return false;
        }

        public string[] NotStartsWith { get; set; }

        public string[] ToRemove { get; set; }

        public string[] ContainsAny { get; set; }

        public string[] ContainsNone { get; set; }

    }

    public class ArgConversion
    {
        string VariablePostfix;
        string SetupTemplate;
        string CleanupTemplate;

        public ArgConversion(string variablePostfix, string setupTemplate, string cleanupTemplate)
        {
            VariablePostfix = variablePostfix;
            SetupTemplate = setupTemplate;
            CleanupTemplate = cleanupTemplate;
        }

        public string GetSetup(string vname)
        {
            return ReplaceVariables(vname, SetupTemplate);
        }

        public string ReplaceVariables(string vname, string template)
        {
            return template.Replace("C_ARGNAME", GetTransientVarname(vname)).Replace("RCPP_ARGNAME", vname);
        }

        public string GetTransientVarname(string vname)
        {
            return vname + VariablePostfix;
        }

        public string GetCleanup(string vname)
        {
            return ReplaceVariables(vname, CleanupTemplate);
        }

    }

    public class StringHelper
    {
        public static string NewLineString = "\n";

        public static string Concat(string[] elemts, int start, int count, string sep = " ")
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count - 1; i++)
            {
                sb.Append(elemts[start + i]);
                sb.Append(sep);
            }
            sb.Append(elemts[start + count - 1]);
            return sb.ToString();
        }

        public static string GetReturnedType(string funDef)
        {
            return GetFunctionTypeAndName(funDef).TypeName;
        }

        public static TypeAndName GetFunctionTypeAndName(string funDef)
        {
            FuncAndArgs funcAndArgs = GetFuncDeclAndArgs(funDef);
            return GetVariableDeclaration(funcAndArgs.Function);
        }

        public static string GetFuncName(string funDef)
        {
            TypeAndName typeAndName = GetFunctionTypeAndName(funDef);
            return typeAndName.VarName;
        }

        public static string[] GetFunctionArguments(string funDef)
        {
            FuncAndArgs funcAndArgs = GetFuncDeclAndArgs(funDef);
            return SplitOnComma(funcAndArgs.Arguments);
        }

        public static string[] SplitOnComma(string functionArguments)
        {
            string[] args = functionArguments.Trim().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //NodeInfo* nodes, int numNodes, LinkInfo* links, int numLinks
            return args;
        }

        public static TypeAndName GetVariableDeclaration(string argString)
        {
            return new TypeAndName(argString);
        }

        public static FuncAndArgs GetFuncDeclAndArgs(string line)
        {
            return new FuncAndArgs(line);
        }

        public static bool ReturnsCharPP(string funDef)
        {
            return (GetReturnedType(funDef) == "char**");
        }

        public static bool appendArgs(StringBuilder sb, Action<StringBuilder, TypeAndName> argFunc, Dictionary<string, string> transientArgs, string[] args, int start, int end, string sep = ", ")
        {
            string arg;
            for (int i = start; i < end; i++)
            {
                arg = args[i];
                if (!addArgument(sb, argFunc, transientArgs, arg)) return false;
                if (i < (end - 1))
                    sb.Append(sep);
            }
            return true;
        }

        public static bool addArgument(StringBuilder sb, Action<StringBuilder, TypeAndName> argFunc, Dictionary<string, string> transientArgs, string arg)
        {
            var typeAndName = StringHelper.GetVariableDeclaration(arg);
            if (typeAndName.Unexpected) return false;
            string vname = typeAndName.VarName;
            if (transientArgs != null && transientArgs.ContainsKey(vname))
            {
                sb.Append(transientArgs[vname]);
                return true;
            }
            return ParseTypeAndName(sb, arg, argFunc);
        }

        public static bool ParseTypeAndName(StringBuilder sb, string argString, Action<StringBuilder, TypeAndName> fun = null)
        {
            // argString could be something like:
            // double x
            // const char* s
            // ModelRunner * s
            var typeAndName = StringHelper.GetVariableDeclaration(argString);

            if (typeAndName.Unexpected) return false;
            fun(sb, typeAndName);
            return true;
        }


    }

    public class TypeAndName
    {
        public TypeAndName(string argString)
        {
            // argString could be something like:
            // double x
            // const char* s
            // ModelRunner * s
            var typeAndName = argString.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);//"ModelRunner*" "CreateNewFromNetworkInfo"
                                                                                                           // cater for things like const char* s:
            if (typeAndName.Length > 2)
                typeAndName = new[]{
                    StringHelper.Concat(typeAndName, 0, typeAndName.Length-1),
                    typeAndName[typeAndName.Length-1]};
            if (typeAndName.Length == 2)
            {
                TypeName = typeAndName[0].Trim();
                VarName = typeAndName[1].Trim();
            }
            else
                Unexpected = true;
        }
        public string TypeName = string.Empty;
        public string VarName = string.Empty;
        public bool Unexpected = false;
    }

    public class FuncAndArgs
    {
        public FuncAndArgs(string s)
        {
            // At this point we'd have:
            //ModelRunner* CreateNewFromNetworkInfo(NodeInfo* nodes, int numNodes, LinkInfo* links, int numLinks);
            // or
            //SWIFT_API MODEL_SIMULATION_PTR CreateNewFromNetworkInfo(NODE_INFO_PTR nodes, int numNodes, LINK_INFO_PTR links, int numLinks);
            s = s.Replace(")", "");
            s = s.Replace(";", "");
            string[] funcAndArgs = s.Split(new[] { '(' }, StringSplitOptions.RemoveEmptyEntries);
            if (funcAndArgs.Length == 0) { Unexpected = true; return; }
            Function = funcAndArgs[0];
            if (funcAndArgs.Length == 1) { return; }
            Arguments = funcAndArgs[1];
            if (funcAndArgs.Length > 2) { Unexpected = true; }
        }
        public string Function = string.Empty;
        public string Arguments = string.Empty;
        public bool Unexpected = false;
    }


    public class RXptrWrapperGenerator : BaseApiConverter, IApiConverter
    {

        public RXptrWrapperGenerator()
        {
            AssignmentSymbol = "<-";
            ReturnedValueVarname = "result";

            ClearCustomWrappers();
            CustomFunctionWrapperImpl cw = ReturnsCharPtrPtrWrapper();
            AddCustomWrapper(cw);

        }

        public CustomFunctionWrapperImpl ReturnsCharPtrPtrWrapper()
        {
            CustomFunctionWrapperImpl cw = new CustomFunctionWrapperImpl()
            {
                IsMatchFunc = StringHelper.ReturnsCharPP,
                ApiArgToRcpp = ApiArgToRfunctionArgument,
                ApiCallArgument = this.ApiCallArgument,
                TransientArgsCreation = this.TransientArgCreation,
                FunctionNamePostfix = this.FunctionNamePostfix,
                CalledFunctionNamePostfix = this.ApiCallPostfix,
                Template = @"
#' docco
%WRAPFUNCTION% <- function(%WRAPARGS%)
{
    %TRANSARGS%
    result <- %FUNCTION%(%WRAPARGS%);
    return(mkSwiftObjRef(result,'char**'))
}
"
            };
            return cw;
        }

        /*

        SWIFT_API OBJECTIVE_EVALUATOR_PTR CreateObjectiveCalculator(MODEL_SIMULATION_PTR modelInstance, char* obsVarId, double * observations,
            int arrayLength, MarshaledDateTime start, char* statisticId);

        CreateObjectiveCalculator_R <- function(modelInstance, obsVarId, observations, arrayLength, start, statisticId) {
            .Call('swift_CreateObjectiveCalculator_R', PACKAGE = 'swift', modelInstance, obsVarId, observations, arrayLength, start, statisticId)
        }

        And we want to generate something like:

        CreateObjectiveCalculator_R_wrap <- function(modelInstance, obsVarId, observations, arrayLength, start, statisticId) {
            modelInstance_xptr <- getSwiftXptr(modelInstance)
            xptr <- CreateObjectiveCalculator_R(modelInstance_xptr, obsVarId, observations, arrayLength, start, statisticId)
            return(mkSwiftObjRef(xptr))
        }

        SWIFT_API_FUNCNAME_R_wrap <- function(modelInstance, obsVarId, observations, arrayLength, start, statisticId) {
            modelInstance_xptr <- getSwiftXptr(modelInstance)
            xptr <- SWIFT_API_FUNCNAME_R(modelInstance_xptr, obsVarId, observations, arrayLength, start, statisticId)
            return(mkSwiftObjRef(xptr))
        }

}

*/
        public override string ConvertApiLine(string line)
        {
            if (MatchesCustomWrapper(line))
                return ApplyCustomWrapper(line);

            var funcAndArgs = StringHelper.GetFuncDeclAndArgs(line);
            if (funcAndArgs.Unexpected) return line; // bail out - just not sure what is going on.
            var sb = new StringBuilder();
            if (!createWrapFuncSignature(sb, funcAndArgs)) return line;
            string result = "";
            result = createWrappingFunctionBody(line, funcAndArgs, sb, ApiCallArgument);
            return result;
        }

        private void TransientArgCreation(StringBuilder sb, TypeAndName typeAndName)
        {
            //    x <- getSwiftXptr(x);
            sb.Append(typeAndName.VarName);
            sb.Append(" <- getSwiftXptr(");
            sb.Append(typeAndName.VarName);
            sb.Append(");");
        }

        private void ApiCallArgument(StringBuilder sb, TypeAndName typeAndName)
        {
            //    xptr <- SWIFT_API_FUNCNAME_R(modelInstance_xptr, obsVarId, observations, arrayLength, start, statisticId)
            // in context:
            //SWIFT_API_FUNCNAME_R_wrap < -function(modelInstance, obsVarId, observations, arrayLength, start, statisticId) {
            //    modelInstance_xptr < -getSwiftXptr(modelInstance)
            //    xptr <- SWIFT_API_FUNCNAME_R(modelInstance_xptr, obsVarId, observations, arrayLength, start, statisticId)
            //    return (mkSwiftObjRef(xptr))
            //}
            sb.Append(typeAndName.VarName);
        }

        private bool createWrapFuncSignature(StringBuilder sb, FuncAndArgs funcAndArgs)
        {
            sb.Append("#' docco" + NewLineString);
            var funcDecl = GetTypeAndName(funcAndArgs.Function);
            string funcDef = funcDecl.VarName + FunctionNamePostfix + " <- function";
            sb.Append(funcDef);
            return AddFunctionArgs(sb, funcAndArgs, ApiArgToRfunctionArgument);
        }

        private void ApiArgToRfunctionArgument(StringBuilder sb, TypeAndName typeAndName)
        {
            sb.Append(typeAndName.VarName);
        }

        protected override void CreateBodyReturnValue(StringBuilder sb, TypeAndName funcDef, bool returnsVal)
        {
            if (returnsVal)
            {
                sb.Append("    return(mkSwiftObjRef(" + ReturnedValueVarname + ",'" + funcDef.TypeName + "'))");
            }
        }

        protected override void AppendReturnedValueDeclaration(StringBuilder sb)
        {
            sb.Append(ReturnedValueVarname);
            sb.Append(" "); sb.Append(AssignmentSymbol); sb.Append(" ");
        }
    }

    public abstract class BaseApiConverter
    {
        protected BaseApiConverter()
        {
            NewLineString = StringHelper.NewLineString;
            FunctionBodyOpenDelimiter = NewLineString + "{" + NewLineString;
            FunctionBodyCloseDelimiter = NewLineString + "}" + NewLineString;
            StatementSep = ";";
            ApiCallPostfix = string.Empty;
        }

        public string ApiCallPostfix { get; set; }

        public string StatementSep { get; set; }

        public string FunctionNamePostfix { get; set; }

        public string[] PointersEndsWithAny { get; set; }

        public string NewLineString { get; set; }

        public string ConvertLine(string line)
        {
            string convertedLine = string.Empty;
            convertedLine += ConvertApiLine(line);
            convertedLine += NewLineString;
            return convertedLine;
        }

        public abstract string ConvertApiLine(string line);

        protected List<CustomFunctionWrapper> customWrappers =
            new List<CustomFunctionWrapper>();

        public string PrependOutputFile { get; set; }

        public string GetPreamble()
        {
            return PrependOutputFile;
        }

        public void ClearCustomWrappers()
        {
            customWrappers.Clear();
        }

        public void AddCustomWrapper(CustomFunctionWrapperImpl cw)
        {
            customWrappers.Add(cw);
        }

        protected static bool AddFunctionArgs(StringBuilder sb, FuncAndArgs funcAndArgs, Action<StringBuilder, TypeAndName> argFunc, Dictionary<string, string> transientArgs = null)
        {
            sb.Append("(");
            string functionArguments = funcAndArgs.Arguments;
            string[] args = StringHelper.SplitOnComma(functionArguments);
            if (args.Length > 0)
            {
                int start = 0, end = args.Length - 1;
                if (!StringHelper.appendArgs(sb, argFunc, transientArgs, args, start, end)) return false;
                if (end > start)
                    sb.Append(", ");
                string arg = args[args.Length - 1];
                if (!StringHelper.addArgument(sb, argFunc, transientArgs, arg)) return false;
            }
            sb.Append(")");
            return true;
        }

        public string ApplyCustomWrapper(string line)
        {
            foreach (var c in customWrappers)
            {
                if (c.IsMatch(line))
                    return c.CreateWrapper(line);
            }
            return line;
        }

        public bool MatchesCustomWrapper(string line)
        {
            foreach (var c in customWrappers)
            {
                if (c.IsMatch(line))
                    return true;
            }
            return false;
        }

        private Dictionary<string, string> typeMap = new Dictionary<string, string>();

        public Dictionary<string, string> TypeMap
        {
            get { return typeMap; }
            set { typeMap = value; }
        }

        public void SetTypeMap(string cType, string rcppType)
        {
            typeMap[cType] = rcppType;
        }

        public bool IsKnownType(string typename)
        {
            return typeMap.ContainsKey(typename);
        }

        public void FindTransientVariables(string functionArguments, out Dictionary<string, string> transientArgs, out string[] transientArgsSetup, out string[] transientArgsCleanup)
        {
            transientArgs = new Dictionary<string, string>();
            List<string> setup = new List<string>(), cleanup = new List<string>();
            string[] args = StringHelper.SplitOnComma(functionArguments);
            for (int i = 0; i < args.Length; i++)
            {
                var s = args[i].Trim();
                if (string.IsNullOrEmpty(s))
                    continue;
                var varDecl = StringHelper.GetVariableDeclaration(s); // "const int*" "blah"
                addTransientVariable(varDecl, transientArgs, setup, cleanup);
            }
            transientArgsSetup = setup.ToArray();
            transientArgsCleanup = cleanup.ToArray();
        }

        protected void FindTransientVariables(StringBuilder sb, FuncAndArgs funcAndArgs, ref Dictionary<string, string> transientArgs, ref string[] transientArgsSetup, ref string[] transientArgsCleanup)
        {
            string functionArguments = funcAndArgs.Arguments;
            FindTransientVariables(functionArguments, out transientArgs, out transientArgsSetup, out transientArgsCleanup);
            foreach (var item in transientArgsSetup)
                sb.AppendLine("    " + item); // e.g. char** linkIdsChar = createAnsiStringArray(linkIds);
        }

        protected static bool FunctionReturnsValue(TypeAndName funcDef)
        {
            return (funcDef.TypeName.Trim() != "void");
        }

        private void addTransientVariable(TypeAndName varDecl, Dictionary<string, string> transientArgs, List<string> setup, List<string> cleanup)
        {
            string tname = varDecl.TypeName;
            string vname = varDecl.VarName;
            ArgConversion confInfo = null;
            if (transientArgConversion.ContainsKey(tname))
                confInfo = transientArgConversion[tname];
            else
            {
                confInfo = matchByRegex(transientArgConversion, tname);
            }
            if (confInfo == null) return;
            setup.Add(confInfo.GetSetup(vname));
            transientArgs.Add(vname, confInfo.GetTransientVarname(vname));
            cleanup.Add(confInfo.GetCleanup(vname));
        }

        private ArgConversion matchByRegex(Dictionary<string, ArgConversion> transientArgConversion, string tname)
        {
            foreach (var converter in transientArgConversion)
            {
                var key = converter.Key;
                if (key.StartsWith(".*")) // KLUDGE, If not doing that, trying with keys such as "char**" causes en exception.
                {
                    var rexpPtr = new Regex(converter.Key);
                    if (rexpPtr.IsMatch(tname))
                        return converter.Value;
                }
            }
            return null;
        }

        // CharacterVector nodeIds
        // char** nodeIdsChar = createAnsiStringArray(nodeIds);
        // freeAnsiStringArray(nodeIdsChar, nodeIds.length());
        private Dictionary<string, ArgConversion> transientArgConversion = new Dictionary<string, ArgConversion>();

        public void SetTransientArgConversion(string cArgType, string variablePostfix, string setupTemplate, string cleanupTemplate)
        {
            transientArgConversion[cArgType] = new ArgConversion(variablePostfix, setupTemplate, cleanupTemplate);
        }

        protected bool createWrappingFunctionSignature(StringBuilder sb, FuncAndArgs funcAndArgs, Action<StringBuilder, TypeAndName> argumentConverterFunction)
        {
            string funcDef = funcAndArgs.Function + FunctionNamePostfix;
            if (!StringHelper.ParseTypeAndName(sb, funcDef, argumentConverterFunction)) return false;
            return AddFunctionArgs(sb, funcAndArgs, argumentConverterFunction);
        }

        public string FunctionBodyOpenDelimiter { get; set; }
        public string FunctionBodyCloseDelimiter { get; set; }

        protected string createWrappingFunctionBody(string line, FuncAndArgs funcAndArgs, StringBuilder sb, Action<StringBuilder, TypeAndName> argFunc)
        {
            string result;
            sb.Append(FunctionBodyOpenDelimiter);
            bool ok = createWrapFuncBody(sb, funcAndArgs, argFunc);
            sb.Append(FunctionBodyCloseDelimiter);
            if (!ok)
                result = line;
            else
                result = sb.ToString();
            return result;
        }

        protected bool createWrapFuncBody(StringBuilder sb, FuncAndArgs funcAndArgs, Action<StringBuilder, TypeAndName> argFunc)
        {
            // We need to cater for cases where we need to create a transient variable then clean it, e.g.
            // char** c = transform((CharacterVector)cvec);
            // apiCall(c)
            // cleanup(c)

            Dictionary<string, string> transientArgs = null;
            string[] transientArgsSetup = null;
            string[] transientArgsCleanup = null;
            FindTransientVariables(sb, funcAndArgs, ref transientArgs, ref transientArgsSetup, ref transientArgsCleanup);

            var funcDef = GetTypeAndName(funcAndArgs.Function);
            bool returnsVal = FunctionReturnsValue(funcDef);
            // 	return XPtr<OpaquePointer>(new OpaquePointer(CloneModel(src->Get())));
            bool ok = CreateApiFunctionCall(sb, funcAndArgs, argFunc, transientArgs, funcDef, returnsVal);
            if (!ok) return false;
            CreateBodyCleanTransientVar(sb, funcAndArgs, transientArgsCleanup);
            CreateBodyReturnValue(sb, funcDef, returnsVal);
            return true;
        }

        protected static TypeAndName GetTypeAndName(string argString)
        {
            return new TypeAndName(argString);
        }

        public bool IsPointer(string typename)
        {
            foreach (string p in PointersEndsWithAny)
                if (typename.EndsWith(p)) return true;
            return false;
        }

        protected static void CreateBodyCleanTransientVar(StringBuilder sb, FuncAndArgs funcAndArgs, string[] transientArgsCleanup)
        {
            foreach (var item in transientArgsCleanup)
                if (!string.IsNullOrEmpty(item))
                    sb.AppendLine("    " + item); // e.g. freeAnsiStringArray(nodeIdsChar, nodeIds.length());
        }

        protected bool CreateApiFunctionCall(StringBuilder sb, FuncAndArgs funcAndArgs, Action<StringBuilder, TypeAndName> argFunc, Dictionary<string, string> transientArgs, TypeAndName funcDef, bool returnsVal)
        {
            sb.Append("    ");
            if (returnsVal) AppendReturnedValueDeclaration(sb);
            sb.Append(funcDef.VarName + ApiCallPostfix);
            if (!AddFunctionArgs(sb, funcAndArgs, argFunc, transientArgs)) return false;
            sb.Append(StatementSep);
            sb.Append(NewLineString);
            return true;
        }

        protected abstract void AppendReturnedValueDeclaration(StringBuilder sb);

        protected abstract void CreateBodyReturnValue(StringBuilder sb, TypeAndName funcDef, bool returnsVal);

        public string AssignmentSymbol { get; set; }

        public string ReturnedValueVarname { get; set; }
    }


    public class RcppGlueWrapperGenerator : BaseApiConverter, IApiConverter
    {

        protected override void AppendReturnedValueDeclaration(StringBuilder sb)
        {
            sb.Append("auto "); sb.Append(ReturnedValueVarname);
            sb.Append(" "); sb.Append(AssignmentSymbol); sb.Append(" ");
        }

        public RcppGlueWrapperGenerator()
        {
            AssignmentSymbol = "=";
            ReturnedValueVarname = "result";
            FunctionNamePostfix = "_R";
            OpaquePointers = false;
            DeclarationOnly = false;
            AddRcppExport = true;
            NewLineString = "\n";

            SetTypeMap("void", "void");
            SetTypeMap("int", "IntegerVector");
            SetTypeMap("int*", "IntegerVector");
            SetTypeMap("char**", "CharacterVector");
            SetTypeMap("char*", "CharacterVector");
            SetTypeMap("char", "CharacterVector");
            SetTypeMap("double", "NumericVector");
            SetTypeMap("double*", "NumericVector");
            SetTypeMap("double**", "NumericMatrix");
            SetTypeMap("bool", "LogicalVector");
            SetTypeMap("const char", "CharacterVector");
            SetTypeMap("const int", "IntegerVector");
            SetTypeMap("const double", "NumericVector");
            SetTypeMap("const char*", "CharacterVector");
            SetTypeMap("const int*", "IntegerVector");
            SetTypeMap("const double*", "NumericVector");

            PointersEndsWithAny = new string[] { "*", "_PTR" };
            OpaquePointerClassName = "OpaquePointer";
            PrependOutputFile = "// This file was GENERATED\n//Do NOT modify it manually, as you are very likely to lose work\n\n";

            ClearCustomWrappers();
            CustomFunctionWrapperImpl cw = ReturnsCharPtrPtrWrapper();
            AddCustomWrapper(cw);
        }

        public CustomFunctionWrapperImpl ReturnsCharPtrPtrWrapper()
        {
            return new CustomFunctionWrapperImpl()
            {
                IsMatchFunc = StringHelper.ReturnsCharPP,
                ApiArgToRcpp = ApiArgToRcpp,
                ApiCallArgument = ApiCallArgument,
                FunctionNamePostfix = this.FunctionNamePostfix,
                Template = @"
// [[Rcpp::export]]
CharacterVector %WRAPFUNCTION%(%WRAPARGS%)
{
	int size; 
	char** names = %FUNCTION%(%ARGS% &size);
	return toVectorCleanup(names, size);
}
"
            };
        }

        public bool DeclarationOnly { get; set; }

        public bool OpaquePointers { get; set; }

        public string OpaquePointerClassName { get; set; }

        public bool AddRcppExport { get; set; }

        public override string ConvertApiLine(string line)
        {
            //SWIFT_API ModelRunner * CloneModel(ModelRunner * src);
            //SWIFT_API ModelRunner * CreateNewFromNetworkInfo(NodeInfo * nodes, int numNodes, LinkInfo * links, int numLinks);
            // And as an output we want for instance (if using opaque pointers).
            // // [[Rcpp::export]]
            // XPtr<OpaquePointer> RcppCloneModel(XPtr<OpaquePointer> src)
            // {
            //     return XPtr<OpaquePointer>(new OpaquePointer(CloneModel(src->Get())));
            // }


            if (MatchesCustomWrapper(line))
                return ApplyCustomWrapper(line);

            var funcAndArgs = StringHelper.GetFuncDeclAndArgs(line);
            if (funcAndArgs.Unexpected) return line; // bail out - just not sure what is going on.
            var sb = new StringBuilder();
            if (!createWrapFuncSignature(sb, funcAndArgs)) return line;
            if (DeclarationOnly)
            {
                sb.Append(StatementSep);
                return sb.ToString();
            }
            else
            {
                string result = "";
                result = createWrappingFunctionBody(line, funcAndArgs, sb, ApiCallArgument);
                return result;
            }
        }

        private bool createWrapFuncSignature(StringBuilder sb, FuncAndArgs funcAndArgs)
        {
            if (AddRcppExport)
                sb.Append("// [[Rcpp::export]]" + NewLineString);
            return createWrappingFunctionSignature(sb, funcAndArgs, ApiArgToRcpp);
        }

        protected override void CreateBodyReturnValue(StringBuilder sb, TypeAndName funcDef, bool returnsVal)
        {
            if (returnsVal)
            {
                sb.Append("    auto x = " + RcppWrap(funcDef.TypeName, ReturnedValueVarname) + StatementSep + NewLineString);
                if (funcDef.TypeName == "char*")
                    sb.Append("    DeleteAnsiString(" + ReturnedValueVarname + ");" + NewLineString);
                sb.Append("    return x;");
            }
        }

        private void ApiArgToRcpp(StringBuilder sb, TypeAndName typeAndName)
        {
            var rt = typeAndName.TypeName;
            ApiTypeToRcpp(sb, rt);
            sb.Append(" ");
            sb.Append(typeAndName.VarName);
        }

        private void ApiCallArgument(StringBuilder sb, TypeAndName typeAndName)
        {
            RcppToApiType(sb, typeAndName.TypeName, typeAndName.VarName);
        }

        private void ApiTypeToRcpp(StringBuilder sb, string typename)
        {
            if (IsKnownType(typename))
                sb.Append(CppToRTypes(typename));
            else if (IsPointer(typename))
                sb.Append(createXPtr(typename)); // XPtr<ModelRunner>
            else
                sb.Append(CppToRTypes(typename));
        }

        private string RcppWrap(string typename, string varname)
        {
            if (IsKnownType(typename))
                return WrapAsRcppVector(typename, varname);
            else if (IsPointer(typename))
                return (createXPtr(typename, varname, true)); // XPtr<ModelRunner>(new OpaquePointer(varname))
            else
                return WrapAsRcppVector(typename, varname);
        }

        private string WrapAsRcppVector(string typename, string varname)
        {
            if (typename == "double" ||
                typename == "int" ||
                typename == "bool")
                return "Rcpp::wrap(" + varname + ")";
            return CppToRTypes(typename) + "(" + varname + ")";
        }

        private void RcppToApiType(StringBuilder sb, string typename, string varname)
        {

            //void SetErrorCorrectionModel_R(XPtr<OpaquePointer> src, CharacterVector newModelId, CharacterVector elementId, IntegerVector length, IntegerVector seed)
            //{
            //    SetErrorCorrectionModel(src->Get(), as<char*>(newModelId), as<char*>(elementId), as<int>(length), as<int>(seed));
            //}
            if (IsKnownType(typename))
                sb.Append(AddAs(typename, varname));
            else if (IsPointer(typename))
            {
                if (typename.EndsWith("**") || typename.EndsWith("PTR*"))
                    sb.Append("(void**)");
                sb.Append(varname + "->Get()"); // src->Get()
            }
            else
                sb.Append(AddAs(typename, varname));
        }

        private string AddAs(string typename, string varname)
        {
            if (typename.EndsWith("char*"))
                return varname + "[0]";
            if (typename.EndsWith("double*") || typename.EndsWith("int*"))
                return "&(" + varname + "[0])";
            return ("as<" + typename + ">(" + varname + ")");
        }

        private string createXPtr(string typePtr, string varname = "", bool instance = false) // ModelRunner* becomes   XPtr<ModelRunner>
        {
            string res;
            if (OpaquePointers)
                res = "XPtr<" + OpaquePointerClassName + ">";
            else
                res = "XPtr<" + typePtr.Replace("*", "") + ">";
            if (instance)
            {
                if (OpaquePointers)
                    res = res + "(new " + OpaquePointerClassName + "(" + varname + "))";
                else
                    res = res + "(" + varname + ")";
            }
            return res;
        }

        private string CppToRTypes(string rt)
        {
            var s = rt.Trim();
            if (TypeMap.ContainsKey(s)) return TypeMap[s]; else return s;
        }


    }

    public interface CustomFunctionWrapper
    {
        string CreateWrapper(string funcDef);
        bool IsMatch(string funcDef);
    }

    public class CustomFunctionWrapperImpl : CustomFunctionWrapper
    {

        public CustomFunctionWrapperImpl()
        {
        }

        public string Template;
        public string argstvar = "%ARGS%";
        public string wrapargstvar = "%WRAPARGS%";
        public string functvar = "%FUNCTION%";
        public string wrapfunctvar = "%WRAPFUNCTION%";
        public string transargtvar = "%TRANSARGS%";
        

        public string FunctionNamePostfix = "";
        public string CalledFunctionNamePostfix = "";
        

        public string CreateWrapper(string funDef)
        {
            string funcName = StringHelper.GetFuncName(funDef);
            string wrapFuncName = funcName + this.FunctionNamePostfix;
            string calledfuncName = funcName + this.CalledFunctionNamePostfix;
            return Template
                .Replace(wrapargstvar, WrapArgsDecl(funDef, 0, 0))
                .Replace(argstvar, FuncCallArgs(funDef, 0, 0))
                .Replace(wrapfunctvar, wrapFuncName)
                .Replace(functvar, calledfuncName)
                .Replace(transargtvar, TransientArgs(funDef, 0, 0));
        }

        public bool IsMatch(string funDef)
        {
            if (IsMatchFunc == null) return false;
            return IsMatchFunc(funDef);
        }

        public Func<string, bool> IsMatchFunc = null;

        // Below are more tricky ones, not yet fully fleshed out support.

        private string WrapArgsDecl(string funDef, int start, int offsetLength)
        {
            if (ApiArgToRcpp == null) return string.Empty;
            return ProcessFunctionArguments(funDef, start, offsetLength, ApiArgToRcpp);
        }

        public Action<StringBuilder, TypeAndName> ApiArgToRcpp = null;
        public Action<StringBuilder, TypeAndName> ApiCallArgument = null;
        public Action<StringBuilder, TypeAndName> TransientArgsCreation = null;
        
        private string TransientArgs(string funDef, int start, int offsetLength)
        {
            if (TransientArgsCreation == null) return string.Empty;
            string result = ProcessFunctionArguments(funDef, start, offsetLength, TransientArgsCreation, appendSeparator: true, sep: StringHelper.NewLineString);
            result += StringHelper.NewLineString;
            return result;
        }

        private string FuncCallArgs(string funDef, int start, int offsetLength)
        {
            if (ApiCallArgument == null) return string.Empty;
            return ProcessFunctionArguments(funDef, start, offsetLength, ApiCallArgument, appendSeparator: true);
        }

        private string ProcessFunctionArguments(string funDef, int start, int offsetLength, Action<StringBuilder, TypeAndName> argFunc, bool appendSeparator = false, string sep=", ")
        {
            StringBuilder sb = new StringBuilder();
            var args = StringHelper.GetFunctionArguments(funDef);
            int end = args.Length - 1 - offsetLength;
            StringHelper.appendArgs(sb, argFunc, null, args, 0, end, sep=sep);
            if (appendSeparator && (end > start)) sb.Append(sep);
            return sb.ToString();
        }
    }
}
