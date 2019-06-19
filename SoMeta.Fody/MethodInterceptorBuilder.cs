using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class MethodInterceptorBuilder
    {
        public MethodDefinition Proceed { get; set; }
        public MethodReference ProceedReference { get; set; }

        private readonly BaseWeaver weaver;
        private readonly MethodDefinition method;
        private readonly ExtensionPointAttribute extensionPoint;

        private MethodReference proceedStructConstructorReference;
        private TypeReference proceedStructType;
        private FieldDefinition instanceField;
        private TypeReference genericType;
        private MethodReference genericProceedTargetMethod;
        private TypeDefinition proceedStruct;
        private MethodDefinition proceedStructConstructor;

        public MethodInterceptorBuilder(BaseWeaver weaver, MethodDefinition method, ExtensionPointAttribute extensionPoint)
        {
            this.weaver = weaver;
            this.method = method;
            this.extensionPoint = extensionPoint;
        }

        public void Build()
        {
            var type = method.DeclaringType;
            genericType = type;
            if (type.HasGenericParameters)
            {
                genericType = type.MakeGenericInstanceType(type.GenericParameters.ToArray());
            }

            var proceedClassName = weaver.GenerateUniqueName(method, extensionPoint.AttributeType, "Proceed");
            proceedStruct = new TypeDefinition(method.DeclaringType.Namespace, proceedClassName, TypeAttributes.NestedPrivate | WeaverContext.Struct, weaver.Context.ValueType);

            proceedStructConstructor = null;
            if (!method.IsStatic)
            {
                instanceField = new FieldDefinition("$this", FieldAttributes.Private, genericType);
                proceedStruct.Fields.Add(instanceField);

                proceedStructConstructor = new MethodDefinition(".ctor", WeaverContext.Constructor, weaver.TypeSystem.VoidReference);
                proceedStructConstructor.Parameters.Add(new ParameterDefinition(genericType));
                proceedStructConstructor.Body = new MethodBody(proceedStructConstructor);
                proceedStructConstructor.Body.InitLocals = true;
                proceedStructConstructor.Body.Emit(il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Stfld, instanceField);
                    il.Emit(OpCodes.Ret);
                });
                proceedStruct.Methods.Add(proceedStructConstructor);
                proceedStructConstructorReference = proceedStructConstructor;
            }
            else
            {
                proceedStructConstructorReference = null;
            }

            method.DeclaringType.CopyGenericParameters(proceedStruct);
            method.CopyGenericParameters(proceedStruct);
            method.DeclaringType.NestedTypes.Add(proceedStruct);

            var original = method.MoveImplementation($"{method.Name}$Original");
            proceedStruct.Methods.Add(Proceed);

            ProceedReference = Proceed;
            TypeReference genericProceedType = proceedStruct;
            if (type.HasGenericParameters || method.HasGenericParameters)
            {
                genericProceedType = proceedStruct.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                ProceedReference = Proceed.Bind((GenericInstanceType)genericProceedType);
                proceedStructConstructorReference = proceedStructConstructor.Bind((GenericInstanceType)genericProceedType);
            }
            proceedStructType = genericProceedType;

            genericProceedTargetMethod = original;

            if (type.HasGenericParameters || method.HasGenericParameters)
            {
                genericProceedTargetMethod = genericProceedTargetMethod.Bind2(genericType,
                    proceedStruct.GenericParameters.Skip(type.GenericParameters.Count).ToArray());//.Bind((GenericInstanceType)genericProceedType);
            }
        }

        public void EmitProceedInstance(ILProcessor il)
        {
            if (!method.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.LoadField(instanceField);                    // Load "this" for when calling "Original"
                il.Emit(OpCodes.Castclass, genericType);
            }
        }

        public void DecomposeArrayIntoArguments(ILProcessor il)
        {
            weaver.DecomposeArrayIntoArguments2(il, proceedStruct, genericProceedTargetMethod, isStatic: false);
        }

        public void EmitCallOriginal(ILProcessor il)
        {
            il.Emit(OpCodes.Call, genericProceedTargetMethod);
        }

        public void EmitProceedStruct(ILProcessor il)
        {
            il.EmitStruct(proceedStructType, proceedStructConstructorReference, () =>
            {
                il.Emit(OpCodes.Ldarg_0);
            });
            il.Emit(OpCodes.Box, proceedStructType);
        }
    }
}