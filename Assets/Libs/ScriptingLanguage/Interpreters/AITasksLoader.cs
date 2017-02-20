﻿using UnityEngine;
using System.Collections;
using System.CodeDom;
using System.Collections.Generic;
using InternalDSL;
using Microsoft.CSharp;
using System.IO;
using System.CodeDom.Compiler;
using System.Reflection;
using System;
using System.Threading;
using System.Text;

public enum TaskState { None, Active, Paused, Failed, Finished }
public abstract class Task
{
	public Penalties Penalties { get;set;}
	public TaskState State {get;set;}
	public GameObject Root { get { return root; } set { root = value; } }
	protected GameObject root;
	public virtual SmartScope AtScope { get {
		return null;
	} }
	protected GameObject at;
	public GameObject At { get { return at; } set {  at = value; } }
	public abstract bool Filter();
	public virtual float Utility(){
		return 0.5f;
	}

	public virtual void Init () {
		State = TaskState.None;
	}
	public virtual InterruptionType Interruption{ get { return InterruptionType.Resumable; } }

	public virtual bool Finished ()
	{
		return true;
	}

	public virtual bool Terminated ()
	{
		return false;
	}

	public virtual string Category
	{
		get {
			return "basic";
		}
	}
	public virtual bool IsBehaviour
	{
		get {
			return true;
		}
	}
}

public abstract class SmartScope
{
    public GameObject CurrentGO;
    public virtual int MaxAttempts { get { return 1; } }
    public int CurAttempts { get { return AlreadyChosenGameObjects.Count; } }
    public List<GameObject> Scope = null;
	public HashSet<GameObject> AlreadyChosenGameObjects = new HashSet<GameObject> ();
	public abstract string FromMetricName { get;  }

	public List<Metric> CachedMetrics;

}
public abstract class ComplexTask : Task
{

	public abstract List<TaskWrapper> Decomposition { get; }
	public virtual void Start () {}
	public override InterruptionType Interruption { get { return InterruptionType.Restartable; } } 
}
public enum InterruptionType { Terminal, Resumable, Restartable }

public abstract class PrimitiveTask : Task
{
	public virtual void OnStart () {}
	public virtual void OnTerminate () {}
	public virtual void OnFinish () {}
	public virtual void OnInterrupt () {}
	public virtual void OnResume () {}
	public virtual void OnUpdate() {}
	public virtual List<TaskWrapper> Dependencies{ get { return null; } }
	public virtual List<TaskWrapper> Constraints { get { return null; } }
	public abstract string Animation { get; }

	public virtual TaskWrapper EngageIn { get { return null; } }
}
public abstract class InteractionTask : PrimitiveTask
{
	public abstract string OtherAnimation { get; }
	public GameObject Other { get { return other; } set { other = value; } }
	protected GameObject other;
	public abstract bool OtherFilter();
	//Используется для авторитарности действия. Если OtherUtility < полезности того, чем занят объект взаимодействия,
	//это значит, что данное взаимодействие сейчас невозможно. 
	//может использоваться, например, для разговора с кем-то. Мол "Я занят", не прерываясь на отдельное действие.
	public virtual float OtherUtility()
	{
		return 1;
	}
}

public partial class AITasksLoader : ScriptInterpreter
{
	List<CodeTypeDeclaration> codeTypes = new List<CodeTypeDeclaration> ();
	FiltersPlugin filters;
	CodeNamespace cNamespace = new CodeNamespace ();
	EventFunctionOperators functionOperators;
	ExpressionInterpreter exprInter;

	public AITasksLoader (string namespaceName, ScriptEngine engine) : base (engine)
	{
		cNamespace.Name = namespaceName;
		exprInter = engine.GetPlugin<ExpressionInterpreter> ();
		filters = engine.GetPlugin<FiltersPlugin> ();
		functionOperators = engine.GetPlugin<EventFunctionOperators> ();
	}
	StringBuilder builder = new StringBuilder();


	public override void Interpret (Script script)
	{
		MaxProgress = script.Entries.Count;

		GenerateAllConditions (script);
		GenerateAllTasks (script);

		CSharpCodeProvider provider = new CSharpCodeProvider ();
		CodeGeneratorOptions options = new CodeGeneratorOptions ();
		var writer = new StringWriter ();
		provider.GenerateCodeFromNamespace (cNamespace, writer, options);
		Engine.GetPlugin<ScriptCompiler> ().AddSource (writer.ToString ());

		Debug.Log (writer.ToString ());
	}


	void GenerateAllConditions(Script script)
	{
		foreach (var entry in script.Entries) {
			if (entry.Identifier is FunctionCall) {
				var call = (entry.Identifier as FunctionCall);
				var type = call.Name;

				var category = call.Args [0].ToString ().ClearFromBraces ().Trim ();
				CodeTypeDeclaration conditionType = null;
				if (type == "dependency") {
					conditionType = CreateDependency (category, entry.Context as Table);
				} else if (type == "constraint") {
					conditionType = CreateConstraint (category, entry.Context as Table);
				} else if (type == "task_wrapper") {
					conditionType = CreateWrapper (category, entry.Context as Table);
				} else {
					//no idea
				}
				cNamespace.Types.Add (conditionType);
			}
		}
	}

	CodeTypeDeclaration CreateDependency(string category, Table table)
	{
		var type = new CodeTypeDeclaration ();
		type.Name = category;
		type.Attributes = MemberAttributes.Public;

		Parameters (type, table);
		Other (type, table);
		InternalProperties (type, table);
		SatisfactionTask (type, table);
		SatisfactionCondition (type, table);
		Serialization (type, table);
		return type;
	}

	CodeTypeDeclaration CreateConstraint(string category, Table table)
	{
		var type = CreateDependency (category, table);
		IsInterruptive (type, table);
		return type;
	}

	CodeTypeDeclaration CreateWrapper(string category, Table table)
	{
		var type = CreateConstraint (category, table);
		When (type, table);
		Attempts (type, table);
		return type;
	}


	void GenerateAllTasks(Script script)
	{

		foreach (var entry in script.Entries) {
			if (!(entry.Identifier is FunctionCall)) {
				var type = entry.Identifier as string;
				var table = entry.Context as Table;
				CodeTypeDeclaration taskType = null;
				if (IsPrimitive (table)) {
					taskType= GeneratePrimitiveTask (type, table);
				} else if (IsComplex(table)) {
					taskType = GenerateComplexTask (type, table);
				}
				if (taskType.UserData.Contains ("init_method")) {
					var initMethod = taskType.GetShared<CodeMemberMethod> ("init");
					initMethod.Statements.Add (taskType.UserData ["init_method"].ToString ().St());
				}
				if (taskType != null)
					cNamespace.Types.Add (taskType);
			}
		}
	}




	#region CreateEventFunction
	void CreateEventFunction (string name, object context, CodeTypeDeclaration codeType, MethodInfo baseMethod, params object[] initStatements)
	{
		CodeMemberMethod method = new CodeMemberMethod ();
		method.Name = NameTranslator.CSharpNameFromScript (name);
		method.Attributes = MemberAttributes.Override | MemberAttributes.Public;
		method.ReturnType = new CodeTypeReference (baseMethod.ReturnType);

		var args = baseMethod.GetParameters ();
		FunctionBlock block = new FunctionBlock (null, null, codeType);
		block.Statements.Add ("var root = this.root;");

        block.Statements.Add("var at = this.at;");
        //block.Statements.Add ("UnityEngine.Debug.Log(root.ToString() + IfStatement.AntiMergeValue++);");
        var externVar = new DeclareVariableStatement()
		{
			Name = "External",
			IsArg = true,
			Type = Engine.GetType("External")
		};
		block.Statements.Add(externVar);
		block.Statements.Add(new ContextStatement()
			{
				ContextVar = externVar,
				InterpretInContext = Engine.GetPlugin<ExternalFunctionsPlugin>().Ctx.InterpretInContext
			});
		foreach (var initStmt in initStatements)
			block.Statements.Add (initStmt);
		//bool hasRoot = false;
		foreach (var arg in args)
		{
			//if (arg.Name == "root")
			//	hasRoot = true;
			method.Parameters.Add (new CodeParameterDeclarationExpression (arg.ParameterType, arg.Name));
			var paramVar = new DeclareVariableStatement ();
			paramVar.Name = arg.Name;
			paramVar.Type = arg.ParameterType;
			paramVar.IsArg = true;
			block.Statements.Add (paramVar);
		}
		var rootVar = new DeclareVariableStatement ();
		rootVar.Name = "root";
		rootVar.Type = typeof(GameObject);
		rootVar.IsArg = true;
		rootVar.IsContext = true;

		block.Statements.Add (rootVar);

        var atVar = new DeclareVariableStatement();
        atVar.Name = "at";
        atVar.Type = typeof(GameObject);
        atVar.IsArg = true;
        atVar.IsContext = false;

        block.Statements.Add(atVar);

        foreach (var member in codeType.Members)
		{
			var field = member as CodeMemberField;
			if (field != null)
			{
				var cachedVar = new DeclareVariableStatement ();
				cachedVar.Name = field.Name;
				cachedVar.Type = field.UserData ["type"] as Type;
				cachedVar.IsArg = true;

				block.Statements.Add (cachedVar);
			}
		}
		//if (!hasRoot)
		//{
		//	Debug.LogFormat ("Method {0} in {1} has no root arg", baseMethod.Name, codeType.Name);
		//	return;
		//}

		codeType.Members.Add (method);
		var table = context as Table;
		if (table != null)
		{
			foreach (var entry in table.Entries)
			{
				Operator op = entry as Operator;
				var inter = functionOperators.GetInterpreter (op, block);
				if (inter == null)
				{
					Debug.LogFormat ("Can't find interpreter for operator {0} in {1} of {2}", op.Identifier, baseMethod.Name, codeType.Name);
					continue;
				}
				inter.Interpret (op, block);
			}	
			var retVal = block.FindStatement<DeclareVariableStatement> (v => v.IsReturn);
			if (retVal != null)
				block.Statements.Add (String.Format ("return {0};", retVal.Name));
		} else
		{
			var expr = context as Expression;

			var retVal = block.FindStatement<DeclareVariableStatement> (v => v.IsReturn);
			//retVal.IsArg = true;
			block.Statements.Add (String.Format ("return ({1}){0};", exprInter.InterpretExpression (expr, block).ExprString, TypeName.NameOf (retVal.Type)));
		}



		method.Statements.Add (new CodeSnippetStatement (block.ToString()));
	}

	#endregion
}

