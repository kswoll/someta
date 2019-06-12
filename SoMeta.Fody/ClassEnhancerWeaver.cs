using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class ClassEnhancerWeaver : BaseWeaver
    {
        private TypeReference injectAccessAttribute;

        public ClassEnhancerWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning) : base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            injectAccessAttribute = moduleDefinition.FindType("SoMeta", "InjectAccessAttribute");
        }

        public void Weave(TypeDefinition type, CustomAttribute interceptor, int attributeIndex, InterceptorScope scope)
        {
            LogInfo($"Weaving class enhancer {interceptor.AttributeType.FullName} at {type.FullName}");

            foreach (var interceptorProperty in interceptor.AttributeType.Resolve().Properties)
            {
                var injectAccess = interceptorProperty.CustomAttributes.SingleOrDefault(x => x.AttributeType.CompareTo(injectAccessAttribute));
                if (injectAccess != null)
                {
//                    Debugger.Launch();
                    var methodName = (string)injectAccess.ConstructorArguments.Single().Value;
                    var targetMethod = type.Methods.Single(x => x.Name == methodName);    // Todo: won't work for overloads

                    // Generate a private static method that forms the accessor that will be assigned
                    // to this property on the associated interceptor in a static initializer.
                    var accessor = new MethodDefinition($"<>__{methodName}$Accessor", MethodAttributes.Private | MethodAttributes.Static, targetMethod.ReturnType);
                    accessor.Parameters.Add(new ParameterDefinition(type));
                    foreach (var parameter in targetMethod.Parameters)
                    {
                        accessor.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
                    }

//                    var delegateType = new TypeDefinition(type.Namespace, accessor.Name + "Type", TypeAttributes.NestedPrivate, Context.DelegateType);
//                    delegateType.Methods.Add(new MethodDefinition("Invoke", MethodAttributes.Public, ))

                    accessor.Body = new MethodBody(accessor);
                    accessor.Body.InitLocals = true;
                    accessor.Body.Emit(il =>
                    {
                        il.Emit(OpCodes.Ldarg_0);

                        for (var i = 1; i < targetMethod.Parameters.Count; i++)
                        {
                            il.EmitArgument(accessor, i);
                        }
                        il.EmitCall(targetMethod);
                    });
                    type.Methods.Add(accessor);

                    // Now set up the static initializer to assign this to the interceptor property
                    type.EmitStaticConstructor(il =>
                    {
                        var isVoid = targetMethod.ReturnType.CompareTo(TypeSystem.VoidReference);
                        var delegateType = (isVoid ? Context.ActionTypes : Context.FuncTypes)[targetMethod.Parameters.Count];
                        var typeArguments = new List<TypeReference>();
                        typeArguments.AddRange(targetMethod.Parameters.Select(x => x.ParameterType));
                        if (!isVoid)
                            typeArguments.Add(targetMethod.ReturnType);

                        il.EmitGetAttributeByIndex(type, attributeIndex, interceptor.AttributeType);
                        il.EmitDelegate(accessor, delegateType, typeArguments.ToArray());
                        il.EmitCall(interceptorProperty.SetMethod);
                    });
                }
            }
        }
    }
}