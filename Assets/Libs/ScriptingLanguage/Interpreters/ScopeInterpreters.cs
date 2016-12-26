﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using InternalDSL;
using System;
using System.Text;

public class HasComponentScope : ScopeInterpreter
{
	public Type Type;

	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{
		Debug.Log ("Has component scope");
		IfStatement ifStatement = new IfStatement ();
		DeclareVariableStatement cmpStmt = new DeclareVariableStatement ();
		cmpStmt.IsTemp = true;
		cmpStmt.IsContext = true;
		ExprInter.CleanUpContextes.Push (cmpStmt);
		cmpStmt.Name = "cmp" + DeclareVariableStatement.VariableId++;
		cmpStmt.Type = Type;
		//cmpStmt.IsContext = true;
		var ctxVar = block.FindStatement<DeclareVariableStatement> (v => v.IsContext);

		string varName = ctxVar == null ? "root" : ctxVar.Name;
		cmpStmt.InitExpression = String.Format ("({1}){0}.GetComponent(typeof({1}))", varName, Type);
		ifStatement.CheckExpression = String.Format ("{0} != null", cmpStmt.Name);
		FunctionBlock newBlock = new FunctionBlock (block, block.Method, block.Type);
		ifStatement.TrueBlock = newBlock;
		block.Statements.Add (cmpStmt);
		block.Statements.Add (ifStatement);
		newCurBlock = newBlock;
		newExprVal = exprVal;
		newContextType = contextType;
		if (isLast)
		{

			var res = block.FindStatement<DeclareVariableStatement> (v => v.IsResult);
			res.Type = typeof(List<>).MakeGenericType (contextType);
			res.InitExpression = String.Format ("new {0}()", TypeName.NameOf (res.Type));
			newExprVal = res.Name;
			newBlock.Statements.Add (String.Format ("{0}.Add({1});", res.Name, varName));
		}

		//ifStatement.CheckExpression = String.Format("{0}.GetComponen")
		//ifStatement.CheckExpression = 
	}
}

[ScopeInterpreter ("fit")]
public class FitScopeInterpreter : ScopeInterpreter
{
	public Type Type;

	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
    {
        if (ScriptEngine.AnalyzeDebug)
            Debug.Log ("fit scope");
		IfStatement ifStatement = new IfStatement ();
//		DeclareVariableStatement cmpStmt = new DeclareVariableStatement ();
//		ExprInter.CleanUpContextes.Add (cmpStmt);
//		cmpStmt.Name = "cmp" + DeclareVariableStatement.VariableId++;
//		cmpStmt.Type = Type;
		//cmpStmt.IsContext = true;
		string varName = block.FindStatement<DeclareVariableStatement> (v => v.IsContext).Name;
//		cmpStmt.InitExpression = String.Format ("{0}.GetComponent<{1}>()", varName, Type);
//		ifStatement.CheckExpression = String.Format ("{0} != null", cmpStmt.Name);
		FunctionBlock newBlock = new FunctionBlock (block, block.Method, block.Type);

		DeclareVariableStatement ifValue = new DeclareVariableStatement ();
		ifValue.Name = "ifResult" + DeclareVariableStatement.VariableId++;
		ifValue.IsTemp = true;
		ifValue.Type = typeof(bool);
		block.Statements.Add (ifValue);
		ifStatement.CheckExpression = (ifValue.Name + " = ") + ExprInter.InterpretExpression (args [0], block).ExprString;

		ifStatement.TrueBlock = newBlock;
		//block.Statements.Add (cmpStmt);
		block.Statements.Add (ifStatement);
		newCurBlock = newBlock;
		newExprVal = exprVal;
		newContextType = contextType;
		if (isLast)
		{

			var res = block.FindStatement<DeclareVariableStatement> (v => v.IsResult);
			if (res != null)
			{
				res.Type = typeof(List<>).MakeGenericType (contextType);
				res.InitExpression = String.Format ("new {0}()", TypeName.NameOf (res.Type));
				newExprVal = res.Name;
				newBlock.Statements.Add (String.Format ("{0}.Add({1});", res.Name, varName));
			} else
			{
				newExprVal = ifValue.Name;
				newContextType = typeof(bool);
			}

		}

		//ifStatement.CheckExpression = String.Format("{0}.GetComponen")
		//ifStatement.CheckExpression = 
	}
}

[ScopeInterpreter ("average")]
public class AverageInterpreter : ScopeInterpreter
{
	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{

		throw new NotImplementedException ();
	}
}

[ScopeInterpreter ("set")]
public class SetScopeInterpreter : ScopeInterpreter
{
	public Type Type;

	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{
		var varName = ((args [0].Operands [0] as ExprAtom).Content as Scope).Parts [0] as string;
		var valueExpr = args [1];
		var exprData = Engine.GetPlugin<ExpressionInterpreter> ().InterpretExpression (valueExpr, block);
		newExprVal = exprVal;
		newCurBlock = block;
		newContextType = contextType;
//		if (exprData.Type != typeof(bool))
//			block.Statements.Add ("return false;");
//		block.Statements.Add (String.Format ("return {0};", exprData.ExprString));
		var declVar = block.FindStatement<DeclareVariableStatement> (v => v.Name == varName && v.Type == exprData.Type);
		if (declVar != null)
		{
			Debug.Log ("Adds set value");
			block.Statements.Add (String.Format ("{0} = {1};", varName, exprData.ExprString));
		} else
		{
			Debug.LogErrorFormat ("Unable to find var {0} in {1} of {2}", varName, block.Method, block.Type);
		}
	}
}

[ScopeInterpreter ("ret")]
public class ReturnScopeInterpreter : ScopeInterpreter
{
	public Type Type;

	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{
		var valueExpr = args [0];
		var exprData = Engine.GetPlugin<ExpressionInterpreter> ().InterpretExpression (valueExpr, block);
		newExprVal = exprData.ExprString;
		newCurBlock = block;
		newContextType = exprData.Type;
	}
}

[ScopeInterpreter ("true")]
public class TrueScopeInterpreter : ScopeInterpreter
{
	public Type Type;

	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{
		var retVar = block.FindStatement<DeclareVariableStatement> (v => v.IsReturn);
		newExprVal = retVar.Name;
		block.Statements.Add (String.Format ("{0} = true;", retVar.Name));
		newCurBlock = block;
		newContextType = typeof(bool);
	}
}

[ScopeInterpreter ("this")]
public class ThisInterpreter : ScopeInterpreter
{
	public override void Interpret (Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
	{
		var thisVar = block.FindStatement<DeclareVariableStatement> (v => v.IsContext && !v.IsTemp);
        //Debug.Log("This var " + thisVar);
		newExprVal = thisVar.Name;
		newCurBlock = block;
		newContextType = thisVar.Type;
	}
}
[ScopeInterpreter("metric")]
public class MetricInterpreter : ScopeInterpreter
{
    public override void Interpret(Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
    {
        
        string rootRef = null;
        if (args.Length == 2)
            rootRef = block.FindStatement<DeclareVariableStatement>(v => v.IsContext && v.Type == typeof(GameObject)).Name;
        else
            rootRef = ExprInter.InterpretExpression(args[1], block).ExprString;

        var metricName = args[0].ToString().ClearFromBraces().Trim(' ');
        var otherRef = ExprInter.InterpretExpression(args.Length == 2? args[1] : args[2], block).ExprString;
        var varName = "metrics" + DeclareVariableStatement.VariableId++;
        block.Statements.Add("var {1} = {0} != null?{0}.GetComponent<Metrics>():null;".Fmt(rootRef, varName));
        newExprVal = "({0} != null? {0}.Value(\"{1}\", {2}) : 0f)".Fmt(varName, metricName, otherRef);
        newContextType = typeof(float);
        newCurBlock = block;
    }
}

[ScopeInterpreter("weighted_metric")]
public class WeightMetricInterpreter : ScopeInterpreter
{
    public override void Interpret(Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
    {

        string rootRef = null;
        if (args.Length == 2)
            rootRef = block.FindStatement<DeclareVariableStatement>(v => v.IsContext && v.Type == typeof(GameObject)).Name;
        else
            rootRef = ExprInter.InterpretExpression(args[1], block).ExprString;

        var metricName = args[0].ToString().ClearFromBraces().Trim(' ');
        var otherRef = ExprInter.InterpretExpression(args.Length == 2 ? args[1] : args[2], block).ExprString;
        var varName = "metrics" + DeclareVariableStatement.VariableId++;
        block.Statements.Add("var {1} = {0} != null?{0}.GetComponent<Metrics>():null;".Fmt(rootRef, varName));
        newExprVal = "({0} != null? {0}.Weight(\"{1}\", {2}) : 0f)".Fmt(varName, metricName, otherRef);
        newContextType = typeof(float);
        newCurBlock = block;
    }
}

[ScopeInterpreter("format")]
public class FormatInterpreter : ScopeInterpreter
{
    StringBuilder codeBuilder = new StringBuilder();
    public override void Interpret(Expression[] args, FunctionBlock block, Type contextType, string exprVal, out string newExprVal, out FunctionBlock newCurBlock, out Type newContextType, bool isLast)
    {
        codeBuilder.Length = 0;
        var dictName = "dict" + DeclareVariableStatement.VariableId++;
        codeBuilder.Append("var ").Append(dictName).Append("= new System.Collections.Generic.Dictionary<string, object>();");
        var strName = "localizedString" + DeclareVariableStatement.VariableId++;
        codeBuilder.AppendLine();
        codeBuilder.Append("var ").Append(strName).Append("= new LocalizedString(");

        codeBuilder.Append(args[0].ToString().ClearFromBraces().Trim(' ')).Append(',').Append(dictName).Append(");");

        for ( int i =1; i < args.Length;i++)
        {
            var paramName = args[i].Operands[0].ToString().ClearFromBraces().Trim(' ');
            var value = ExprInter.InterpretExpression((args[i].Operands[2] as ExprAtom).Content as Expression, block).ExprString;
            codeBuilder.Append(dictName).Append(".Add(\"").Append(paramName).Append("\",").Append(value).AppendLine(");");

        }
        block.Statements.Add(codeBuilder.ToString());
        newExprVal = strName;
        newCurBlock = block;
        newContextType = typeof(LocalizedString);
    }
}