using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SPP4
{
    class Logger
    {
        static void Main(string[] args)
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location).ToString();
            LoggerAttribute afl = new LoggerAttribute("");
            afl.InjectToAssembly(path);
        }

        public class LoggerAttribute : Attribute
        {

            public string attribute { get; set; }
            public LoggerAttribute(string name)
            {
                attribute = name;
            }
            public virtual void OnEnter(System.Reflection.MethodBase method, Dictionary<string, object> parameters)
            {
                WriteLog(method, parameters);
            }

            private void WriteLog(MethodBase method, Dictionary<string, object> parameters)
            {
                string baseString = "CLASS: {" + method.DeclaringType.Name + "}. METHOD: {" + method.Name + "}. PARAMETERS: { " + GetParameters(parameters) + "}";
                System.IO.File.AppendAllText("logger.txt", baseString + Environment.NewLine);
            }

            private string GetStringLog(object returnValue)
            {
                Type returnValueType = returnValue.GetType();
                if ((returnValueType.IsValueType || returnValueType.IsClass) && !returnValueType.Namespace.Contains("System"))
                    return String.Format("{0}: {{{1}}}", returnValueType.ToString(), GetParameters(GetDictionary(returnValue, returnValueType)));
                return returnValue.ToString();
            }

            public string GetParameters(Dictionary<string, object> dictParams)
            {
                string parameters = "";
                if (dictParams.Count != 0)
                {
                    foreach (var s in dictParams)
                    {
                        parameters += s.Key + "=" + dictParams[s.Key] + " ";
                    }
                }
                else
                    parameters = "Nothing";
                return parameters;
            }

            private Dictionary<string, object> GetDictionary(object returnValue, Type returnValueType)
            {
                var parameters = new Dictionary<string, object>();
                FieldInfo[] fieldInfo = returnValueType.GetFields(BindingFlags.Public
                                                                          | BindingFlags.Instance
                                                                          | BindingFlags.NonPublic
                                                                          | BindingFlags.Static);
                PropertyInfo[] propertyInfo = returnValueType.GetProperties(BindingFlags.Public
                                                                          | BindingFlags.Instance
                                                                          | BindingFlags.NonPublic
                                                                          | BindingFlags.Static);
                int fieldLength = fieldInfo.Length;
                int propertyLength = propertyInfo.Length;
                for (int i = 0; i < fieldLength + propertyLength; i++)
                    if (i < fieldInfo.Length)
                        parameters.Add(fieldInfo[i].Name, fieldInfo[i].GetValue(returnValue));
                    else
                        parameters.Add(propertyInfo[i - fieldLength].Name, propertyInfo[i - fieldLength].GetValue(returnValue));
                return parameters;
            }

            public virtual void OnExit(object returnValue)
            {
                Type returnValueType = returnValue.GetType();

                System.IO.File.AppendAllText("logger.txt", "RETURN: {" + GetStringLog(returnValue) + "}" + Environment.NewLine);
            }

            private void GetValue(MethodDefinition method, ILProcessor ilProc, TypeReference objectRef, MethodReference logAttributeOnExitRef, VariableDefinition attributeVar)
            {
                var Value = new VariableDefinition(objectRef);
                ilProc.Body.Variables.Add(Value);
                Instruction lastInstruction = ilProc.Body.Instructions.Last();
                if (!method.ReturnType.Name.Equals(typeof(void).Name))
                {
                    var localVar = method.Body.Variables.First(var => var.VariableType.Name.Equals(method.ReturnType.Name));
                    ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Ldloc, localVar));
                    if (localVar.VariableType.IsPrimitive || localVar.VariableType.IsValueType)
                        ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Box, localVar.VariableType));
                    ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Stloc, Value));
                    ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Ldloc, attributeVar));
                    ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Ldloc, Value));
                    ilProc.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Callvirt, logAttributeOnExitRef));
                }
            }
            public void InjectToAssembly(string path)
            {
                var assembly = AssemblyDefinition.ReadAssembly(path);
                // ссылка на GetCurrentMethod()
                var getCurrentMethodRef = assembly.MainModule.Import(typeof(System.Reflection.MethodBase).GetMethod("GetCurrentMethod"));
                // ссылка на Attribute.GetCustomAttribute()
                var getCustomAttributeRef = assembly.MainModule.Import(typeof(System.Attribute).GetMethod("GetCustomAttribute", new Type[] { typeof(System.Reflection.MethodInfo), typeof(Type) }));
                // ссылка на Type.GetTypeFromHandle() - аналог typeof()
                var getTypeFromHandleRef = assembly.MainModule.Import(typeof(Type).GetMethod("GetTypeFromHandle"));
                // ссылка на тип MethodBase 
                var methodBaseRef = assembly.MainModule.Import(typeof(System.Reflection.MethodBase));
                // ссылка на тип LoggerAttribute
                var interceptionAttributeRef = assembly.MainModule.Import(typeof(LoggerAttribute));
                // ссылка на LoggerAttribute.OnEnter
                var interceptionAttributeOnEnter = assembly.MainModule.Import(typeof(LoggerAttribute).GetMethod("OnEnter"));
                // ссылка на LoggerAttribute.OnExit
                var interceptionAttributeOnExit = assembly.MainModule.Import(typeof(LoggerAttribute).GetMethod("OnExit"));
                // ссылка на тип Dictionary<string,object>
                var dictionaryType = Type.GetType("System.Collections.Generic.Dictionary`2[System.String,System.Object]");
                var dictStringObjectRef = assembly.MainModule.Import(dictionaryType);
                var objectRef = assembly.MainModule.Import(typeof(object));
                var dictConstructorRef = assembly.MainModule.Import(dictionaryType.GetConstructor(Type.EmptyTypes));
                var dictMethodAddRef = assembly.MainModule.Import(dictionaryType.GetMethod("Add"));
                foreach (var typeDef in assembly.MainModule.Types)
                {

                    foreach (var method in typeDef.Methods)
                    {
                        var ilProc = method.Body.GetILProcessor();
                        // необходимо установить InitLocals в true, так как если он находился в false (в методе изначально не было локальных переменных)
                        // а теперь локальные переменные появятся - верификатор IL кода выдаст ошибку.
                        method.Body.InitLocals = true;
                        // создаем локальную переменную для attribute,  и parameters
                        var attributeVariable = new VariableDefinition(interceptionAttributeRef);
                        foreach (var customAttribute in method.CustomAttributes)
                        {
                            string attribute = customAttribute.AttributeType.Name;
                            if (attribute == "LoggerAttribute")
                            {
                                attributeVariable = new VariableDefinition(interceptionAttributeRef);
                                // создаем локальную переменную для currentMethod
                                var currentMethodVar = new VariableDefinition(methodBaseRef);
                                // создаем локальную переменную для parameters
                                var parametersVariable = new VariableDefinition(dictStringObjectRef);
                                ilProc.Body.Variables.Add(attributeVariable);
                                ilProc.Body.Variables.Add(currentMethodVar);
                                ilProc.Body.Variables.Add(parametersVariable);
                                Instruction firstInstruction = ilProc.Body.Instructions[0];
                                //ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Nop));
                                // получаем текущий метод
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, getCurrentMethodRef));
                                // помещаем результат со стека в переменную currentMethodVar
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Stloc, currentMethodVar));
                                // загружаем на стек ссылку на текущий метод
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldloc, currentMethodVar));
                                // загружаем ссылку на тип MethodInterceptionAttribute
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldtoken, interceptionAttributeRef));
                                // Вызываем GetTypeFromHandle (в него транслируется typeof()) - эквивалент typeof(LoggerAttribute)
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, getTypeFromHandleRef));
                                // теперь у нас на стеке текущий метод и тип LoggerAttribute. Вызываем Attribute.GetCustomAttribute
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, getCustomAttributeRef));
                                // приводим результат к типу LoggerAttribute
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Castclass, interceptionAttributeRef));
                                // сохраняем в локальной переменной attributeVariable
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Stloc, attributeVariable));
                                // создаем новый Dictionary<stirng, object>
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Newobj, dictConstructorRef));
                                // помещаем в parametersVariable
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Stloc, parametersVariable));
                                foreach (var argument in method.Parameters)
                                {
                                    //для каждого аргумента метода
                                    // загружаем на стек наш Dictionary<string,object>
                                    ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldloc, parametersVariable));
                                    // загружаем имя аргумента
                                    ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldstr, argument.Name));
                                    // загружаем значение аргумента
                                    ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldarg, argument));
                                    if (argument.ParameterType.IsByReference)
                                        ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldind_I4));
                                    if (argument.ParameterType.GetElementType().IsPrimitive)
                                        ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Box, argument.ParameterType.GetElementType()));
                                    // вызываем Dictionary.Add(string key, object value)
                                    ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, dictMethodAddRef));
                                }
                                // загружаем на стек сначала атрибут, потом параметры для вызова его метода OnEnter
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldloc, attributeVariable));
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldloc, currentMethodVar));
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldloc, parametersVariable));
                                // вызываем OnEnter. На стеке должен быть объект, на котором вызывается OnEnter и параметры метода
                                ilProc.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Callvirt, interceptionAttributeOnEnter));
                            }

                        }
                        GetValue(method, ilProc, objectRef, interceptionAttributeOnExit, attributeVariable);
                    }

                }

                assembly.Write(path);
            }
        }
    }
}
